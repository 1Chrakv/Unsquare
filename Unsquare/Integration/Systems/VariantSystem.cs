using System;
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Naming;
using Unsquare.Core.Options;
using Unsquare.Core.Persistence;
using Unsquare.Core.Scheduling;
using Unsquare.Integration.Assets;
using Unsquare.Integration.Entities;
using Unsquare.Integration.Import;
using Unsquare.Integration.Meshes;
using Unsquare.Integration.PlacemakerCompatibility;
using Unsquare.Integration.Prefabs;
using Unsquare.Integration.Prefabs.Attachments;
using Unsquare.Integration.Saves;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Orchestrates debounce → bake → wait for prefab init → sim copy → swap, plus restore and housekeeping.
    /// Runs in PrefabUpdate before PrefabInitializeSystem and disables itself while idle.
    /// </summary>
    public partial class VariantSystem : GameSystemBase, IVariantPipeline
    {
        private readonly Dictionary<Entity, PendingSwap> _swaps = new();
        private readonly Dictionary<Entity, VariantStatus> _statuses = new();
        private readonly Dictionary<ObjectGeometryPrefab, (CageBox Lot, CageBox Building)> _boxes = new();
        private readonly List<KeyValuePair<Entity, DeformSpec>> _due = new();
        private readonly List<(Entity Building, bool Succeeded)> _finished = new();
        private readonly Dictionary<Entity, ObjectGeometryPrefab> _simDataPending = new();
        private readonly List<Entity> _readyPrefabs = new();
        private readonly Dictionary<Entity, ComponentType[]> _archetypeGaps = new();
        private readonly List<ComponentType> _addedTypes = new();
        private readonly List<Entity> _extensions = new();
        private readonly List<Entity> _describedExtensions = new();
        private readonly Dictionary<BuildingExtensionPrefab, CageBox?> _extensionBoxes = new();
        private readonly HashSet<Entity> _upgradesLogged = new();
        private readonly HashSet<Entity> _extensionRefreshes = new();
        private readonly List<Entity> _refreshing = new();
        private readonly Dictionary<Entity, PendingLevelUp> _levelUps = new();
        private readonly List<Entity> _levelUpsDone = new();
        private readonly Debouncer<Entity, DeformSpec> _debouncer = new(PipelineOptions.Default.RebakeDelaySeconds);
        private readonly SwapGroups<Entity> _swapGroups = new();
        private readonly HashSet<Entity> _variantPrefabs = new();
        private Func<Entity, SwapMemberState> _swapState;
        private IForeignPrefabs _foreign;

        // How long the swaps of a group (e.g. a converted row) wait for the slowest bake before going alone.
        private const double SwapGroupTimeoutSeconds = 10;

        private PrefabSystem _prefabSystem;
        private CityConfigurationSystem _citySystem;
        private PrefabResolver _resolver;
        private GeometryAssetStore _store;
        private SourceMeshCache _sourceMeshes;
        private RenderVariantBuilder _renderBuilder;
        private VariantPrefabFactory _factory;
        private VariantLocalization _localization;
        private PipelineOptions _options = PipelineOptions.Default;
        private EntityQuery _buildingQuery;
        private bool _stateLoaded;
        private bool _simulationReady;
        private Action<Entity> _buildingChanged;

        event Action<Entity> IVariantPipeline.BuildingChanged
        {
            add => _buildingChanged += value;
            remove => _buildingChanged -= value;
        }

        internal IVariantPipeline Pipeline => this;

        /// <summary>Placemaker's bent buildings and their takeover; see <see cref="PlacemakerAdoptions"/>.</summary>
        internal PlacemakerAdoptions Placemaker { get; private set; }

        PipelineOptions IVariantPipeline.Options
        {
            get => _options;
            set
            {
                _options = value ?? throw new ArgumentNullException(nameof(value));
                _debouncer.DelaySeconds = value.RebakeDelaySeconds;
                if (_sourceMeshes != null)
                    _sourceMeshes.BudgetVertices = value.SourceMeshBudgetVertices;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _citySystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            _resolver = new PrefabResolver(_prefabSystem);
            _store = new GeometryAssetStore();
            _sourceMeshes = new SourceMeshCache(_options.SourceMeshBudgetVertices);
            _renderBuilder = new RenderVariantBuilder(_store, _sourceMeshes);
            _factory = new VariantPrefabFactory(_prefabSystem, _renderBuilder,
                new IAttachmentDeformer[]
                {
                    new SubObjectDeformer(), new SubAreaDeformer(), new SubLaneDeformer(), new SubNetDeformer(),
                });
            _localization = new VariantLocalization(GameManager.instance?.localizationManager);
            _swapState = SwapState;
            Placemaker = new PlacemakerAdoptions(World, _prefabSystem, TryGetBoxes, () => _options.Limits);
            _foreign = Placemaker;
            _buildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            _savedCageQuery = GetEntityQuery(
                ComponentType.ReadOnly<SavedCage>(), ComponentType.ReadOnly<Building>(),
                ComponentType.ReadOnly<PrefabRef>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            Enabled = false; // woken by Request
        }

        protected override void OnDestroy()
        {
            _localization?.Dispose();
            _sourceMeshes?.ReleaseAll();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            // A save that threw between moving buildings to their base and restoring them.
            if (SaveSwaps.Count > 0)
                Mod.Log.Warn($"Restored {SaveSwaps.RestoreAll(EntityManager)} buildings left on their base by a save.");
            if (_savedCagesPending)
            {
                _savedCagesPending = false;
                RestoreSavedCagesSafely();
            }
            _due.Clear();
            _debouncer.CollectDue(UnityEngine.Time.realtimeSinceStartupAsDouble, _due);
            foreach (var request in _due)
                Bake(request.Key, request.Value);
            ProcessExtensionRefreshes();
            ProcessLevelUps();

            ProcessSimData();
            ProcessSwaps();
            RepairCopies();
            SaveIfDirty();
            Enabled = _debouncer.Count > 0 || _swaps.Count > 0 || _simDataPending.Count > 0 ||
                      _extensionRefreshes.Count > 0 || _levelUps.Count > 0;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _debouncer.Clear();
            _swaps.Clear();
            _statuses.Clear();
            _simDataPending.Clear();
            _archetypeGaps.Clear();
            _extensionRefreshes.Clear();
            _levelUps.Clear();
            _upgradesLogged.Clear();
            _foreign.Clear();
            _swapGroups.Clear();
            _unsavableLogged.Clear();
            SaveSwaps.Clear();
            _savedCagesPending = false;
            _simulationReady = false;

            // First preload of the session: nothing is registered yet, so pruning cannot strand a live prefab.
            if (!_stateLoaded)
            {
                bool trusted = LoadState();
                _legacy.ArchiveEntries(); // before the prune, so pruned entries stay recoverable
                if (trusted)
                    RunHousekeeping();
                _stateLoaded = true;
            }
            RestoreVariants();
            _sourceMeshes.ReleaseAll(); // the previous city's edits and the restore's misses
            SaveIfDirty();
        }

        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            _simulationReady = (mode & GameMode.GameOrEditor) != 0;
            // Restored variants: copy before the UI or simulation reads them; the rest waits for OnUpdate.
            ProcessSimData();
            RepairCopies();
            if (_simDataPending.Count > 0)
                Enabled = true;
            if (_simulationReady)
                _foreign.OnLoaded();
            if (mode == GameMode.Game && !string.IsNullOrEmpty(_citySystem.cityName))
            {
                _legacy.Manifest.MarkUsage(_citySystem.cityName, CollectUsedVariantKeys(), DateTime.UtcNow);
                SaveIfDirty();
            }
            if (mode == GameMode.Game)
                AnnounceLegacySave();
        }

        bool IVariantPipeline.TryDescribe(Entity building, out BuildingDescription description) =>
            TryDescribe(building, out description);

        bool IVariantPipeline.TryGetRequested(Entity building, out DeformSpec spec)
        {
            if (_debouncer.TryPeek(building, out spec))
                return true;
            bool swapping = _swaps.TryGetValue(building, out var swap);
            spec = swapping ? swap.Spec : null;
            return swapping;
        }

        VariantStatus IVariantPipeline.GetStatus(Entity building) =>
            _statuses.TryGetValue(building, out var status) ? status : VariantStatus.Idle;

        void IVariantPipeline.Request(Entity building, DeformSpec spec)
        {
            if (!TryDescribe(building, out var description))
                return;
            spec = (spec ?? DeformSpec.Identity).ForVariant(); // the base prefab has no terrain flag
            _swaps.Remove(building);

            if (spec.Equals(description.Applied))
            {
                _debouncer.Cancel(building);
                // Taken over, then reset before the swap: still foreign, so the next pick asks again.
                _foreign.Release(building);
                SetStatus(building, VariantStatus.Idle);
                return;
            }
            if (spec.IsIdentity && _prefabSystem.TryGetEntity(description.BasePrefab, out var baseEntity))
            {
                _debouncer.Cancel(building); // reverting needs no bake
                QueueSwap(building, baseEntity, description.BasePrefab, spec,
                    PlanExtensionTargets(building, description.BasePrefab, spec, null));
            }
            else
            {
                _debouncer.Request(building, spec, UnityEngine.Time.realtimeSinceStartupAsDouble);
                SetStatus(building, VariantStatus.Pending);
            }
            Enabled = true;
        }

        void IVariantPipeline.BakeNow(Entity building)
        {
            if (_debouncer.Release(building))
                Enabled = true;
        }

        int IVariantPipeline.ResetAll()
        {
            _debouncer.Clear();
            _swaps.Clear();
            _statuses.Clear();
            _swapGroups.Clear();
            _foreign.ReleaseAll(); // every takeover in flight is cancelled

            int count = 0;
            using var buildings = _buildingQuery.ToEntityArray(Allocator.Temp);
            foreach (var building in buildings)
            {
                if (!_resolver.TryGetPrefab(EntityManager, building, out var current) ||
                    !VariantNaming.IsVariantName(current.name) ||
                    !_resolver.TryResolveBase(current, out var basePrefab, out _) ||
                    !_prefabSystem.TryGetEntity(basePrefab, out var baseEntity))
                    continue;
                var netRemap = BuildNetRemap(current, basePrefab, DeformSpec.Identity);
                var extensions = BuildExtensionSwaps(
                    PlanExtensionTargets(building, basePrefab, DeformSpec.Identity, null), netRemap, basePrefab,
                    DeformSpec.Identity);
                if (PrefabSwapper.Swap(EntityManager, building, baseEntity, extensions, _options.AreaMatchMeters,
                        netRemap))
                {
                    count++;
                    _buildingChanged?.Invoke(building);
                }
            }
            Mod.Log.Info($"Reset {count} deformed buildings.");
            return count;
        }

        bool IVariantPipeline.TryRebase(Entity building, ObjectGeometryPrefab basePrefab)
        {
            if (!_simulationReady || !_prefabSystem.TryGetEntity(basePrefab, out var baseEntity))
                return false;
            _debouncer.Cancel(building);
            _swaps.Remove(building);
            try
            {
                // Foreign variants never moved the lot's nets: an identity remap keeps them in place.
                PrefabSwapper.Swap(EntityManager, building, baseEntity, _options.AreaMatchMeters,
                    new LotRemap(null, null));
                SetStatus(building, VariantStatus.Idle);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Moving building {building} to {basePrefab.name} failed.");
                return false;
            }
        }

        FlexItImportBatch IVariantPipeline.PrepareFlexItImport()
        {
            if (!_simulationReady)
            {
                Mod.Log.Warn("FlexIt import needs a loaded city.");
                return new FlexItImportBatch { NoCity = true };
            }
            return new FlexItImporter(World, _prefabSystem, this, _buildingQuery).Prepare();
        }

        /// <summary>Lot cage and deformation a variant prefab was baked from; false for base prefabs and unknown variants.</summary>
        internal bool TryGetVariantCage(Entity prefabEntity, out CageBox lotBox, out DeformSpec spec)
        {
            lotBox = default;
            spec = null;
            if (!_prefabSystem.TryGetPrefab(prefabEntity, out PrefabBase prefab) || prefab == null ||
                !VariantNaming.IsVariantName(prefab.name) ||
                !_resolver.TryGetVariant(prefab.name, out var info) ||
                !TryGetBoxes(info.BasePrefab, out lotBox, out _))
                return false;
            spec = info.Spec;
            return true;
        }

        // Same deformations the variants were baked with; null if the current variant's cage is unknown.
        private LotRemap BuildNetRemap(PrefabBase current, ObjectGeometryPrefab basePrefab, DeformSpec target)
        {
            if (!TryGetBoxes(basePrefab, out var lotBox, out var buildingBox))
                return null;
            IDeformation from = null;
            if (VariantNaming.IsVariantName(current.name))
            {
                if (!_resolver.TryGetVariant(current.name, out var info))
                    return null;
                from = Deformation(lotBox, buildingBox, info.Spec);
            }
            return new LotRemap(from, Deformation(lotBox, buildingBox, target));
        }

        // Nets follow the cages like paths (SubNetDeformer): the building's at its footprint, the lot's beyond.
        private static IDeformation Deformation(CageBox lotBox, CageBox buildingBox, DeformSpec spec) =>
            spec == null || spec.IsIdentity ? null : SplitDeformations.Create(lotBox, buildingBox, spec).Ground;

        private bool TryDescribe(Entity building, out BuildingDescription description)
        {
            description = default;
            if (!EntityManager.HasComponent<Building>(building) ||
                !_resolver.TryGetPrefab(EntityManager, building, out var current) ||
                !TryResolveBase(building, current, out var basePrefab, out var applied))
                return false;
            return TryDescribe(building, basePrefab, applied, out description);
        }

        private bool TryDescribe(Entity building, ObjectGeometryPrefab basePrefab, DeformSpec applied,
            out BuildingDescription description)
        {
            description = default;
            if (!TryGetBoxes(basePrefab, out var lotBox, out var buildingBox))
                return false;
            DescribeExtensions(building, buildingBox, out float extensionsTop, out var buildingEditBox);
            description = new BuildingDescription(basePrefab, lotBox, ExtendedCage.RaiseTop(lotBox, extensionsTop),
                buildingBox, buildingEditBox, !buildingBox.Equals(lotBox), applied);
            return true;
        }

        // Buildings on another mod's variants (Placemaker's) are only described once taken over, on the plain base;
        // undecided ones are not described, so nothing edits them. Unsquare variants resolve by name as always.
        private bool TryResolveBase(Entity building, PrefabBase current, out ObjectGeometryPrefab basePrefab,
            out DeformSpec applied)
        {
            if (_foreign.IsForeignPrefab(building, current))
            {
                applied = DeformSpec.Identity;
                return _foreign.TryGetTakenOverBase(building, out basePrefab);
            }
            return _resolver.TryResolveBase(current, out basePrefab, out applied);
        }

        void IVariantPipeline.SwapTogether(IReadOnlyCollection<Entity> buildings)
        {
            if (buildings.Count < 2)
                return;
            _swapGroups.Add(buildings, UnityEngine.Time.realtimeSinceStartupAsDouble + SwapGroupTimeoutSeconds);
            Enabled = true;
        }

        // Hover preview only: the cage taking the building over would give, without taking it over.
        bool IVariantPipeline.TryDescribeForeign(Entity building, out BuildingDescription description)
        {
            description = default;
            return _foreign.TryGetPreview(building, out var basePrefab, out var spec) &&
                   TryDescribe(building, basePrefab, spec, out description);
        }

        // Over the on-lot extensions the owner's cage deforms (PlanExtensionTargets), in building space: the top of
        // all of them, and the building box grown over those baked with the building cage.
        private void DescribeExtensions(Entity building, CageBox buildingBox, out float top,
            out CageBox buildingEditBox)
        {
            top = float.MinValue;
            buildingEditBox = buildingBox;
            InstalledExtensions.Collect(EntityManager, building, _describedExtensions);
            foreach (var extension in _describedExtensions)
            {
                if (!TryGetExtensionBase(extension, out var extensionBase) ||
                    !IsDeformedExtension(building, extension, extensionBase, out _) ||
                    !(ExtensionBox(extensionBase) is CageBox box))
                    continue;
                top = math.max(top, box.Max.y);
                if (ExtensionCage.FollowsBuilding(box, float3.zero, buildingBox))
                    buildingEditBox = ExtendedCage.Enclose(buildingEditBox, box);
            }
        }

        // The extension's mesh box in its owner's building space; null without meshes.
        private CageBox? ExtensionBox(BuildingExtensionPrefab extensionBase)
        {
            if (!_extensionBoxes.TryGetValue(extensionBase, out var box))
            {
                box = CageBoxMeasurer.TryMeasureMeshes(extensionBase, out var meshBox)
                    ? meshBox.RelativeTo(-extensionBase.m_Position)
                    : null;
                _extensionBoxes[extensionBase] = box;
            }
            return box;
        }

        private bool TryGetBoxes(ObjectGeometryPrefab basePrefab, out CageBox lotBox, out CageBox buildingBox)
        {
            if (_boxes.TryGetValue(basePrefab, out var boxes))
            {
                (lotBox, buildingBox) = boxes;
                return true;
            }
            if (!CageBoxMeasurer.TryMeasureLot(basePrefab, out lotBox))
            {
                buildingBox = default;
                return false;
            }
            // Flat or meshless lots (parks) have no facades; props then align to the lot edges.
            if (!CageBoxMeasurer.TryMeasureMeshes(basePrefab, out buildingBox))
                buildingBox = lotBox;
            _boxes[basePrefab] = (lotBox, buildingBox);
            return true;
        }

        private void Bake(Entity building, DeformSpec spec)
        {
            if (!TryDescribe(building, out var description))
            {
                SetStatus(building, VariantStatus.Idle);
                return;
            }
            var stopwatch = Stopwatch.StartNew();
            int createdBefore = _renderBuilder.CreatedAssetCount;
            bool created = TryGetOrCreateVariant(description.BasePrefab, spec, VariantOrigin.Edit, out var variant);
            stopwatch.Stop();
            // 0 new assets means everything was reused (existing prefab or cached geometry).
            Mod.Log.Info($"Bake for building {building}: {(created ? variant.name : "failed")}, " +
                         $"{stopwatch.ElapsedMilliseconds} ms, {_renderBuilder.CreatedAssetCount - createdBefore} new assets, " +
                         $"sources held {_sourceMeshes.Count} ({_sourceMeshes.HeldVertices} vertices)");

            if (created && _prefabSystem.TryGetEntity(variant, out var variantEntity))
                QueueSwap(building, variantEntity, description.BasePrefab, spec,
                    PlanExtensionTargets(building, description.BasePrefab, spec, variant.name));
            else
                SetStatus(building, VariantStatus.Failed);
        }

        private bool TryGetOrCreateVariant(ObjectGeometryPrefab basePrefab, DeformSpec spec, VariantOrigin origin,
            out ObjectGeometryPrefab variant)
        {
            variant = null;
            string typeName = basePrefab.GetType().Name;
            string variantName = VariantNaming.VariantName(basePrefab.name, spec);
            if (_resolver.TryFind(typeName, variantName, out var existing))
            {
                variant = existing as ObjectGeometryPrefab;
                if (variant != null)
                {
                    _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, spec));
                    _localization.Register(variantName, basePrefab.name);
                    if (_prefabSystem.TryGetEntity(variant, out var existingEntity))
                        _variantPrefabs.Add(existingEntity);
                }
                return variant != null;
            }
            if (!TryGetBoxes(basePrefab, out var box, out var buildingBox))
            {
                Mod.Log.Warn($"No lot cage box for {basePrefab.name}.");
                return false;
            }

            // Restores skip validation: a save already holds the shape, refusing it breaks the building.
            if (origin == VariantOrigin.Edit &&
                CageValidator.ValidateSplit(box, buildingBox, spec, _options.Limits) != CageValidity.Valid)
            {
                Mod.Log.Warn($"Rejected invalid cage for {basePrefab.name}: {spec}");
                return false;
            }

            var assets = new HashSet<string>(StringComparer.Ordinal);
            if (!_factory.TryCreate(basePrefab, variantName, box, buildingBox, float3.zero, spec, false,
                    _options.Attachments, _options.CoarseLodTwistMeters, assets, out variant))
                return false;
            _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, spec));
            _localization.Register(variantName, basePrefab.name);
            // Restored variants are never swapped to, so the copy cannot wait for TrySwap.
            if (_prefabSystem.TryGetEntity(variant, out var variantEntity))
            {
                _simDataPending[variantEntity] = basePrefab;
                _variantPrefabs.Add(variantEntity);
            }

            // The manifest is frozen: only legacy entries complete their asset lists, so their assets stay claimed.
            if (origin == VariantOrigin.Legacy)
                _legacy.Manifest.MergeAssets(ManifestEntry.MakeKey(typeName, variantName), assets);
            return true;
        }

        /// <summary>
        /// Bakes newly installed on-lot extensions of a deformed building with its current cage (next update, in the
        /// prefab phase). Buildings with a pending request are skipped: that request plans their extensions anyway.
        /// </summary>
        internal void RequestExtensionRefresh(Entity building)
        {
            if (_extensionRefreshes.Add(building))
                Enabled = true;
        }

        private void ProcessExtensionRefreshes()
        {
            if (_extensionRefreshes.Count == 0)
                return;
            _refreshing.Clear();
            _refreshing.AddRange(_extensionRefreshes);
            _extensionRefreshes.Clear();
            foreach (var building in _refreshing)
            {
                if (_swaps.ContainsKey(building) || _debouncer.TryPeek(building, out _) ||
                    !TryDescribe(building, out var description) || description.Applied.IsIdentity ||
                    !_resolver.TryGetPrefab(EntityManager, building, out var current) ||
                    !_prefabSystem.TryGetEntity(current, out var currentEntity))
                    continue;
                Mod.Log.Info($"New upgrade on deformed building {building}: deforming its extensions.");
                QueueSwap(building, currentEntity, description.BasePrefab, description.Applied,
                    PlanExtensionTargets(building, description.BasePrefab, description.Applied, current.name));
            }
        }

        /// <summary>
        /// A deformed growable levels up to the plain <paramref name="targetPrefab"/>: bakes it with the cage rescaled
        /// to its height (<see cref="LevelUpCarry"/>) and swaps the building to it once the game finished the level-up.
        /// </summary>
        internal void PrepareLevelUp(Entity building, Entity fromPrefab, Entity targetPrefab, CageBox fromLot,
            DeformSpec spec)
        {
            if (!_options.KeepOnLevelUp || _levelUps.ContainsKey(building))
                return;
            _levelUps[building] = new PendingLevelUp(fromPrefab, targetPrefab, fromLot, spec);
            Enabled = true;
        }

        private void ProcessLevelUps()
        {
            if (_levelUps.Count == 0)
                return;
            _levelUpsDone.Clear();
            foreach (var pair in _levelUps)
            {
                Entity building = pair.Key;
                PendingLevelUp levelUp = pair.Value;
                if (!EntityManager.Exists(building) || EntityManager.HasComponent<Deleted>(building) ||
                    !EntityManager.TryGetComponent(building, out PrefabRef prefabRef))
                {
                    _levelUpsDone.Add(building);
                    continue;
                }
                if (!levelUp.Baked)
                    BakeLevelUp(building, levelUp);
                if (prefabRef.m_Prefab == levelUp.From)
                    continue; // the game has not swapped the prefab yet
                if (prefabRef.m_Prefab == levelUp.Target && !IsLotSettled(building))
                    continue; // its new areas and nets are generated later this frame; remap them once they exist
                // Any other prefab means someone else replaced the building meanwhile: leave it alone.
                if (prefabRef.m_Prefab == levelUp.Target && levelUp.Variant != Entity.Null)
                {
                    Mod.Log.Info($"Building {building} levelled up: carrying its cage over as {levelUp.VariantName}.");
                    QueueSwap(building, levelUp.Variant, levelUp.TargetBase, levelUp.Carried,
                        PlanExtensionTargets(building, levelUp.TargetBase, levelUp.Carried, levelUp.VariantName));
                }
                _levelUpsDone.Add(building);
            }
            foreach (var building in _levelUpsDone)
                _levelUps.Remove(building);
        }

        // The level-up's Updated lasts until cleanup; by then Modification1 has created the new prefab's lot items.
        private bool IsLotSettled(Entity building)
        {
            if (EntityManager.HasComponent<Updated>(building))
                return false;
            if (EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Game.Areas.SubArea> areas))
            {
                for (int i = 0; i < areas.Length; i++)
                {
                    if (IsInFlux(areas[i].m_Area))
                        return false;
                }
            }
            if (EntityManager.TryGetBuffer(building, true, out DynamicBuffer<Game.Net.SubNet> nets))
            {
                for (int i = 0; i < nets.Length; i++)
                {
                    if (IsInFlux(nets[i].m_SubNet))
                        return false;
                }
            }
            return true;
        }

        private bool IsInFlux(Entity item) =>
            EntityManager.HasComponent<Created>(item) || EntityManager.HasComponent<Deleted>(item);

        // Runs once per level-up: a cage that doesn't fit the new building leaves it plain instead of retrying.
        private void BakeLevelUp(Entity building, PendingLevelUp levelUp)
        {
            levelUp.Baked = true;
            try
            {
                if (!_prefabSystem.TryGetPrefab(levelUp.Target, out PrefabBase prefab) ||
                    !(prefab is ObjectGeometryPrefab target) || !TryGetBoxes(target, out var toLot, out _))
                {
                    Mod.Log.Warn($"Building {building} levels up to an unknown prefab; its cage is not carried over.");
                    return;
                }
                var carried = LevelUpCarry.Rescale(levelUp.Spec, levelUp.FromLot, toLot).ForVariant();
                if (carried.IsIdentity)
                    return;
                if (TryGetOrCreateVariant(target, carried, VariantOrigin.Edit, out var variant) &&
                    _prefabSystem.TryGetEntity(variant, out var variantEntity))
                    levelUp.SetVariant(target, carried, variantEntity, variant.name);
                else
                    Mod.Log.Warn($"Building {building} levels up to {target.name}, which can't take its cage " +
                                 $"({carried}); it stays undeformed.");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Carrying the cage of building {building} over its level-up failed.");
            }
        }

        // Base targets when ownerVariantName is null; an extension that can't be deformed goes back to its base.
        private List<(Entity Extension, Entity Target)> PlanExtensionTargets(Entity building,
            ObjectGeometryPrefab ownerBase, DeformSpec spec, string ownerVariantName,
            VariantOrigin origin = VariantOrigin.Edit)
        {
            var targets = new List<(Entity, Entity)>();
            InstalledExtensions.Collect(EntityManager, building, _extensions);
            foreach (var extension in _extensions)
            {
                if (!TryGetExtensionBase(extension, out var extensionBase))
                    continue;
                ObjectGeometryPrefab target = extensionBase;
                if (ownerVariantName != null && IsDeformable(extensionBase))
                {
                    if (!IsDeformedExtension(building, extension, extensionBase, out var offset))
                        Mod.Log.Warn($"Extension {extensionBase.name} of building {building} was moved (at {offset}, " +
                                     $"prefab {extensionBase.m_Position}); it stays undeformed.");
                    else if (TryGetOrCreateExtensionVariant(ownerBase, spec, ownerVariantName, extensionBase, origin,
                                 out var variant))
                        target = variant;
                }
                if (_prefabSystem.TryGetEntity(target, out var targetEntity))
                    targets.Add((extension, targetEntity));
            }
            return targets;
        }

        private bool TryGetExtensionBase(Entity extension, out BuildingExtensionPrefab extensionBase)
        {
            extensionBase = null;
            if (!_resolver.TryGetPrefab(EntityManager, extension, out var current))
                return false;
            if (!VariantNaming.TryParse(current.name, out string baseName))
                extensionBase = current as BuildingExtensionPrefab;
            else if (_resolver.TryFind(current.GetType().Name, baseName, out var found))
                extensionBase = found as BuildingExtensionPrefab;
            return extensionBase != null;
        }

        // Extensions the owner's cage bends: deformable and still where the prefab puts them.
        private bool IsDeformedExtension(Entity building, Entity extension, BuildingExtensionPrefab extensionBase,
            out float3 offset)
        {
            offset = default;
            return IsDeformable(extensionBase) && InstalledExtensions.IsAtPrefabPlacement(EntityManager, building,
                extension, extensionBase.m_Position, out offset);
        }

        private bool IsOverBuilding(BuildingExtensionPrefab extension, CageBox ownerBuildingBox) =>
            ExtensionBox(extension) is CageBox box && ExtensionCage.FollowsBuilding(box, float3.zero, ownerBuildingBox);

        // External lots lie outside the cage; option-only upgrades have nothing to bend.
        private static bool IsDeformable(BuildingExtensionPrefab extension) =>
            !extension.m_ExternalLot &&
            (extension.m_Meshes is { Length: > 0 } || extension.TryGet(out ObjectSubObjects _) ||
             extension.TryGet(out ObjectSubAreas _) || extension.TryGet(out ObjectSubLanes _) ||
             extension.TryGet(out ObjectSubNets _));

        /// <summary>Bakes an upgrade extension with its owner's cage, in its own frame on the owner's lot.</summary>
        private bool TryGetOrCreateExtensionVariant(ObjectGeometryPrefab ownerBase, DeformSpec spec,
            string ownerVariantName, BuildingExtensionPrefab extensionBase, VariantOrigin origin,
            out ObjectGeometryPrefab variant)
        {
            variant = null;
            string typeName = extensionBase.GetType().Name;
            string variantName = VariantNaming.ExtensionVariantName(extensionBase.name, ownerVariantName);
            if (_resolver.TryFind(typeName, variantName, out var existing))
            {
                variant = existing as ObjectGeometryPrefab;
                if (variant != null)
                    _localization.Register(variantName, extensionBase.name);
                return variant != null;
            }
            if (!TryGetBoxes(ownerBase, out var box, out var buildingBox))
                return false;

            var assets = new HashSet<string>(StringComparer.Ordinal);
            if (!_factory.TryCreate(extensionBase, variantName, box, buildingBox, extensionBase.m_Position, spec,
                    !IsOverBuilding(extensionBase, buildingBox), _options.Attachments, _options.CoarseLodTwistMeters,
                    assets, out variant))
                return false;
            _localization.Register(variantName, extensionBase.name);
            if (_prefabSystem.TryGetEntity(variant, out var variantEntity))
                _simDataPending[variantEntity] = extensionBase;
            if (origin == VariantOrigin.Legacy)
                _legacy.Manifest.AddExtension(ManifestEntry.MakeKey(ownerBase.GetType().Name, ownerVariantName),
                    ManifestEntry.MakeKey(typeName, extensionBase.name), assets);
            return true;
        }

        // Extension nets laid out by an older variant follow the building's remap; base ones start undeformed.
        private List<ExtensionSwap> BuildExtensionSwaps(List<(Entity Extension, Entity Target)> targets,
            LotRemap buildingRemap, ObjectGeometryPrefab ownerBase, DeformSpec spec)
        {
            var swaps = new List<ExtensionSwap>(targets.Count);
            foreach (var (extension, target) in targets)
            {
                LotRemap remap = null;
                if (_resolver.TryGetPrefab(EntityManager, extension, out var current))
                {
                    if (VariantNaming.IsVariantName(current.name))
                        remap = buildingRemap;
                    else if (TryGetBoxes(ownerBase, out var lotBox, out var buildingBox))
                        remap = new LotRemap(null, Deformation(lotBox, buildingBox, spec));
                }
                swaps.Add(new ExtensionSwap(extension, target, remap));
            }
            return swaps;
        }

        private void QueueSwap(Entity building, Entity targetPrefab, ObjectGeometryPrefab basePrefab, DeformSpec spec,
            List<(Entity Extension, Entity Target)> extensions, bool syncLot = true, Entity expectedCurrent = default)
        {
            _swaps[building] = new PendingSwap(targetPrefab, basePrefab, spec, extensions, syncLot, expectedCurrent);
            SetStatus(building, VariantStatus.WaitingForPrefab);
        }

        private void ProcessSimData()
        {
            if (_simDataPending.Count == 0)
                return;
            _readyPrefabs.Clear();
            foreach (var pair in _simDataPending)
            {
                // A new prefab entity keeps Created until PrefabInitializeSystem has filled its data.
                if (!EntityManager.Exists(pair.Key) || !EntityManager.HasComponent<Created>(pair.Key))
                    _readyPrefabs.Add(pair.Key);
            }

            int copied = 0, completed = 0;
            foreach (var variantEntity in _readyPrefabs)
            {
                var basePrefab = _simDataPending[variantEntity];
                _simDataPending.Remove(variantEntity);
                try
                {
                    if (EntityManager.Exists(variantEntity) && _prefabSystem.TryGetEntity(basePrefab, out var donor))
                    {
                        if (PrepareVariantPrefab(donor, variantEntity))
                            completed++;
                        copied++;
                    }
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Copying sim data from {basePrefab.name} to variant {variantEntity} failed.");
                }
            }
            if (copied > 0)
                Mod.Log.Info($"Copied sim data to {copied} variants ({completed} archetypes completed), " +
                             $"{_simDataPending.Count} still initializing.");
        }

        // Everything a variant prefab needs from its base once initialized; each step is safe to repeat.
        private bool PrepareVariantPrefab(Entity donor, Entity variant)
        {
            SimDataCopier.CopyMissing(EntityManager, donor, variant);
            UpgradeRegistration.RemoveFromBuildings(EntityManager, variant);
            bool completed = VariantArchetype.TryComplete(EntityManager, donor, variant, _addedTypes);
            if (completed)
                _archetypeGaps[variant] = _addedTypes.ToArray();
            UpgradeOwnership.Share(EntityManager, _prefabSystem, donor, variant);
            return completed;
        }

        // Buildings created from a variant before its archetype was completed, e.g. copies in older saves.
        private void RepairCopies()
        {
            if (_archetypeGaps.Count == 0)
                return;
            try
            {
                using var buildings = _buildingQuery.ToEntityArray(Allocator.Temp);
                int repaired = VariantInstanceRepair.AddMissing(EntityManager, buildings, _archetypeGaps);
                if (repaired > 0)
                    Mod.Log.Info($"Repaired {repaired} copies of variants missing instance components.");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Repairing copies of variants failed.");
            }
            _archetypeGaps.Clear();
        }

        private void ProcessSwaps()
        {
            _finished.Clear();
            if (_swapGroups.Count > 0 &&
                _swapGroups.Update(_swapState, UnityEngine.Time.realtimeSinceStartupAsDouble) > 0)
                Mod.Log.Warn($"Swapping a group after {SwapGroupTimeoutSeconds} s without all of its bakes.");
            foreach (var pair in _swaps)
            {
                Entity building = pair.Key;
                PendingSwap swap = pair.Value;
                if (!EntityManager.Exists(building) || EntityManager.HasComponent<Deleted>(building))
                {
                    _finished.Add((building, true));
                    continue;
                }
                if (_swapGroups.IsHeld(building))
                    continue;
                // A new prefab entity keeps Created until PrefabInitializeSystem has filled its data.
                if (!_simulationReady || !IsInitialized(swap))
                    continue;
                _finished.Add((building, TrySwap(building, swap)));
            }

            foreach (var (building, succeeded) in _finished)
            {
                _swaps.Remove(building);
                // Off the foreign variant now: if the other mod bends it again later, picking it asks again.
                if (succeeded)
                    _foreign.Release(building);
                SetStatus(building, succeeded ? VariantStatus.Idle : VariantStatus.Failed);
            }
        }

        // Swaps held together: a member is ready once its swap could run, done once nothing is in flight for it.
        private SwapMemberState SwapState(Entity building)
        {
            if (_swaps.TryGetValue(building, out var swap))
                return !EntityManager.Exists(building) || EntityManager.HasComponent<Deleted>(building) ||
                       (_simulationReady && IsInitialized(swap))
                    ? SwapMemberState.Ready
                    : SwapMemberState.Waiting;
            return _debouncer.TryPeek(building, out _) ? SwapMemberState.Waiting : SwapMemberState.Done;
        }

        private bool IsInitialized(PendingSwap swap)
        {
            if (EntityManager.HasComponent<Created>(swap.TargetPrefab))
                return false;
            foreach (var (_, target) in swap.Extensions)
            {
                if (EntityManager.HasComponent<Created>(target))
                    return false;
            }
            return true;
        }

        private bool TrySwap(Entity building, PendingSwap swap)
        {
            try
            {
                if (swap.ExpectedCurrent != Entity.Null &&
                    EntityManager.TryGetComponent(building, out PrefabRef now) && now.m_Prefab != swap.ExpectedCurrent)
                    return HandOverLoadSwap(building, swap, now.m_Prefab);
                if (!HasInstanceArchetype(swap))
                    return false;
                if (_prefabSystem.TryGetEntity(swap.BasePrefab, out var donor) && donor != swap.TargetPrefab)
                    PrepareVariantPrefab(donor, swap.TargetPrefab);
                var netRemap = swap.SyncLot && _resolver.TryGetPrefab(EntityManager, building, out var currentPrefab)
                    ? BuildNetRemap(currentPrefab, swap.BasePrefab, swap.Spec)
                    : null;
                LogUpgradesOnce(building);
                var extensions = BuildExtensionSwaps(swap.Extensions, netRemap, swap.BasePrefab, swap.Spec);
                PrefabSwapper.Swap(EntityManager, building, swap.TargetPrefab, extensions, _options.AreaMatchMeters,
                    netRemap, swap.SyncLot);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Swapping prefab of building {building} failed.");
                return false;
            }
        }

        // A prefab the game never initialized has no instance archetype; buildings on it crash the game natively.
        private bool HasInstanceArchetype(PendingSwap swap)
        {
            bool Valid(Entity prefab) =>
                EntityManager.TryGetComponent(prefab, out ObjectData data) && data.m_Archetype.Valid;

            bool valid = Valid(swap.TargetPrefab);
            foreach (var (_, target) in swap.Extensions)
                valid &= Valid(target);
            if (!valid)
                Mod.Log.Error($"Not swapping to {swap.TargetPrefab} ({swap.BasePrefab.name}): the game did not initialize it.");
            return valid;
        }

        private void LogUpgradesOnce(Entity building)
        {
            if (!_upgradesLogged.Add(building))
                return;
            string upgrades = InstalledExtensions.Describe(EntityManager, _prefabSystem, building);
            if (upgrades != null)
                Mod.Log.Info($"Upgrades of building {building}:{upgrades}");
        }

        // Idle always notifies: the applied offsets may have changed even if the status did not.
        private void SetStatus(Entity building, VariantStatus status)
        {
            if (status == VariantStatus.Idle)
                _statuses.Remove(building);
            else if (_statuses.TryGetValue(building, out var old) && old == status)
                return;
            else
                _statuses[building] = status;
            _buildingChanged?.Invoke(building);
        }

        private void SaveIfDirty()
        {
            try
            {
                _legacy.SaveIfDirty();
                if (_renderBuilder.BoundsCache.IsDirty)
                    _renderBuilder.BoundsCache.Save(ModPaths.BoundsCacheFile);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Saving variant manifest failed.");
            }
        }

        private readonly struct PendingSwap
        {
            public PendingSwap(Entity targetPrefab, ObjectGeometryPrefab basePrefab, DeformSpec spec,
                List<(Entity Extension, Entity Target)> extensions, bool syncLot, Entity expectedCurrent)
            {
                TargetPrefab = targetPrefab;
                BasePrefab = basePrefab;
                Spec = spec;
                Extensions = extensions;
                SyncLot = syncLot;
                ExpectedCurrent = expectedCurrent;
            }

            public Entity TargetPrefab { get; }
            public ObjectGeometryPrefab BasePrefab { get; }
            public DeformSpec Spec { get; }
            public List<(Entity Extension, Entity Target)> Extensions { get; }

            /// <summary>False when the saved areas and nets already have the target's layout (a load).</summary>
            public bool SyncLot { get; }

            /// <summary>Prefab the building must still be on; Entity.Null to swap from any.</summary>
            public Entity ExpectedCurrent { get; }
        }

        private sealed class PendingLevelUp
        {
            public PendingLevelUp(Entity from, Entity target, CageBox fromLot, DeformSpec spec)
            {
                From = from;
                Target = target;
                FromLot = fromLot;
                Spec = spec;
            }

            /// <summary>The variant the building was on and the plain prefab the game levels it up to.</summary>
            public Entity From { get; }
            public Entity Target { get; }
            public CageBox FromLot { get; }
            public DeformSpec Spec { get; }

            public bool Baked { get; set; }
            public ObjectGeometryPrefab TargetBase { get; private set; }
            public DeformSpec Carried { get; private set; }
            public Entity Variant { get; private set; }
            public string VariantName { get; private set; }

            public void SetVariant(ObjectGeometryPrefab targetBase, DeformSpec carried, Entity variant, string name)
            {
                TargetBase = targetBase;
                Carried = carried;
                Variant = variant;
                VariantName = name;
            }
        }
    }
}
