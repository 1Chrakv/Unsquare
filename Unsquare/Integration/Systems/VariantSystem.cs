using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Unsquare.Core.Diagnostics;
using Unsquare.Core.Naming;
using Unsquare.Core.Options;
using Unsquare.Core.Persistence;
using Unsquare.Core.Scheduling;
using Unsquare.Integration.Assets;
using Unsquare.Integration.Entities;
using Unsquare.Integration.Import;
using Unsquare.Integration.Prefabs;
using Unsquare.Integration.Prefabs.Attachments;

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

        private PrefabSystem _prefabSystem;
        private CityConfigurationSystem _citySystem;
        private PrefabResolver _resolver;
        private readonly PhaseTimer _bakeTimer = new();
        private GeometryAssetStore _store;
        private RenderVariantBuilder _renderBuilder;
        private VariantPrefabFactory _factory;
        private VariantLocalization _localization;
        private VariantManifest _manifest = new();
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

        PipelineOptions IVariantPipeline.Options
        {
            get => _options;
            set
            {
                _options = value ?? throw new ArgumentNullException(nameof(value));
                _debouncer.DelaySeconds = value.RebakeDelaySeconds;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _citySystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            _resolver = new PrefabResolver(_prefabSystem);
            _store = new GeometryAssetStore(_bakeTimer);
            _renderBuilder = new RenderVariantBuilder(_store, _bakeTimer);
            _factory = new VariantPrefabFactory(_prefabSystem, _renderBuilder,
                new IAttachmentDeformer[]
                {
                    new SubObjectDeformer(), new SubAreaDeformer(), new SubLaneDeformer(), new SubNetDeformer(),
                }, _bakeTimer);
            _localization = new VariantLocalization(GameManager.instance?.localizationManager);
            _buildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            Enabled = false; // woken by Request
        }

        protected override void OnDestroy()
        {
            _localization?.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
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
            _simulationReady = false;

            // First preload of the session: nothing is registered yet, so pruning cannot strand a live prefab.
            if (!_stateLoaded)
            {
                bool trusted = LoadState();
                if (trusted)
                    RunHousekeeping();
                _stateLoaded = true;
            }
            RestoreVariants();
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
            if (mode == GameMode.Game && !string.IsNullOrEmpty(_citySystem.cityName))
            {
                _manifest.MarkUsage(_citySystem.cityName, CollectUsedVariantKeys(), DateTime.UtcNow);
                SaveIfDirty();
            }
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

        int IVariantPipeline.ResetAll()
        {
            _debouncer.Clear();
            _swaps.Clear();
            _statuses.Clear();

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
                !_resolver.TryResolveBase(current, out var basePrefab, out var applied) ||
                !TryGetBoxes(basePrefab, out var lotBox, out var buildingBox))
                return false;
            DescribeExtensions(building, buildingBox, out float extensionsTop, out var buildingEditBox);
            description = new BuildingDescription(basePrefab, lotBox, ExtendedCage.RaiseTop(lotBox, extensionsTop),
                buildingBox, buildingEditBox, !buildingBox.Equals(lotBox), applied);
            return true;
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
            _bakeTimer.Reset();
            var stopwatch = Stopwatch.StartNew();
            int createdBefore = _renderBuilder.CreatedAssetCount;
            bool created = TryGetOrCreateVariant(description.BasePrefab, spec, isRestore: false, out var variant);
            stopwatch.Stop();
            // 0 new assets means everything was reused (existing prefab or cached geometry).
            Mod.Log.Info($"Bake for building {building}: {(created ? variant.name : "failed")}, " +
                         $"{stopwatch.ElapsedMilliseconds} ms, {_renderBuilder.CreatedAssetCount - createdBefore} new assets");
            Mod.Log.Info($"Bake phases: {_bakeTimer.Summary()}");

            if (created && _prefabSystem.TryGetEntity(variant, out var variantEntity))
                QueueSwap(building, variantEntity, description.BasePrefab, spec,
                    PlanExtensionTargets(building, description.BasePrefab, spec, variant.name));
            else
                SetStatus(building, VariantStatus.Failed);
        }

        private bool TryGetOrCreateVariant(ObjectGeometryPrefab basePrefab, DeformSpec spec, bool isRestore,
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
                }
                return variant != null;
            }
            if (!TryGetBoxes(basePrefab, out var box, out var buildingBox))
            {
                Mod.Log.Warn($"No lot cage box for {basePrefab.name}.");
                return false;
            }

            // Restores skip validation: a save already references the variant, refusing it breaks the building.
            if (!isRestore && CageValidator.ValidateSplit(box, buildingBox, spec, _options.Limits) != CageValidity.Valid)
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
                _simDataPending[variantEntity] = basePrefab;

            string key = ManifestEntry.MakeKey(typeName, variantName);
            if (isRestore)
            {
                _manifest.MergeAssets(key, assets);
                return true;
            }
            var entry = new ManifestEntry
            {
                BaseType = typeName,
                BaseName = basePrefab.name,
                VariantName = variantName,
                Assets = new List<string>(assets),
            };
            entry.SetSpec(spec);
            using (_bakeTimer.Measure(BakePhase.Manifest))
                _manifest.Register(entry, _citySystem.cityName, DateTime.UtcNow);
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
                if (TryGetOrCreateVariant(target, carried, isRestore: false, out var variant) &&
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
            ObjectGeometryPrefab ownerBase, DeformSpec spec, string ownerVariantName)
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
                    else if (TryGetOrCreateExtensionVariant(ownerBase, spec, ownerVariantName, extensionBase,
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
            string ownerVariantName, BuildingExtensionPrefab extensionBase, out ObjectGeometryPrefab variant)
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
            _manifest.AddExtension(ManifestEntry.MakeKey(ownerBase.GetType().Name, ownerVariantName),
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
            List<(Entity Extension, Entity Target)> extensions)
        {
            _swaps[building] = new PendingSwap(targetPrefab, basePrefab, spec, extensions);
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
            foreach (var pair in _swaps)
            {
                Entity building = pair.Key;
                PendingSwap swap = pair.Value;
                if (!EntityManager.Exists(building) || EntityManager.HasComponent<Deleted>(building))
                {
                    _finished.Add((building, true));
                    continue;
                }
                // A new prefab entity keeps Created until PrefabInitializeSystem has filled its data.
                if (!_simulationReady || !IsInitialized(swap))
                    continue;
                _finished.Add((building, TrySwap(building, swap)));
            }

            foreach (var (building, succeeded) in _finished)
            {
                _swaps.Remove(building);
                SetStatus(building, succeeded ? VariantStatus.Idle : VariantStatus.Failed);
            }
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
                var stopwatch = Stopwatch.StartNew();
                if (_prefabSystem.TryGetEntity(swap.BasePrefab, out var donor) && donor != swap.TargetPrefab)
                    PrepareVariantPrefab(donor, swap.TargetPrefab);
                double prepared = stopwatch.Elapsed.TotalMilliseconds;
                var netRemap = _resolver.TryGetPrefab(EntityManager, building, out var currentPrefab)
                    ? BuildNetRemap(currentPrefab, swap.BasePrefab, swap.Spec)
                    : null;
                LogUpgradesOnce(building);
                var extensions = BuildExtensionSwaps(swap.Extensions, netRemap, swap.BasePrefab, swap.Spec);
                double remapped = stopwatch.Elapsed.TotalMilliseconds;
                PrefabSwapper.Swap(EntityManager, building, swap.TargetPrefab, extensions, _options.AreaMatchMeters,
                    netRemap);
                double swapped = stopwatch.Elapsed.TotalMilliseconds;
                Mod.Log.Info($"Swap phases for building {building}: prepare {prepared:0.0}, " +
                             $"remap {remapped - prepared:0.0}, swap {swapped - remapped:0.0} ms");
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Swapping prefab of building {building} failed.");
                return false;
            }
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

        private bool LoadState()
        {
            var manifestStatus = VariantManifest.Load(ModPaths.ManifestFile, out _manifest);
            if (_manifest.DiscardedOutdatedCount > 0)
                Mod.Log.Warn($"Variant manifest has an old format: discarded {_manifest.DiscardedOutdatedCount} variants. " +
                             "Deformed buildings in older saves revert or break; delete ModsData/Unsquare to free space.");
            BoundsCache.Load(ModPaths.BoundsCacheFile, out var bounds);
            _renderBuilder.BoundsCache = bounds;

            switch (manifestStatus)
            {
                case JsonReadStatus.Corrupt:
                    Mod.Log.Error("Variant manifest is unreadable; deformed buildings in saves may break.");
                    PreserveCorruptManifest();
                    return false;
                case JsonReadStatus.FromBackup:
                    Mod.Log.Warn("Variant manifest restored from backup.");
                    return false;
                default:
                    return true;
            }
        }

        private void RunHousekeeping()
        {
            try
            {
                new VariantHousekeeper(_store).Run(_manifest, _renderBuilder.BoundsCache, DateTime.UtcNow,
                    _options.PruneUnused ? _options.PruneAfter : (TimeSpan?)null);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Housekeeping failed.");
            }
        }

        private void RestoreVariants()
        {
            int restored = 0, failed = 0, extensions = 0;
            foreach (var entry in new List<ManifestEntry>(_manifest.Entries))
            {
                try
                {
                    var spec = entry.ToSpec();
                    if (_resolver.TryFind(entry.BaseType, entry.BaseName, out var found) &&
                        found is ObjectGeometryPrefab basePrefab &&
                        TryGetOrCreateVariant(basePrefab, spec, isRestore: true, out var variant))
                    {
                        restored++;
                        extensions += RestoreExtensions(entry, basePrefab, spec, variant.name);
                    }
                    else
                        failed++;
                }
                catch (Exception e)
                {
                    failed++;
                    Mod.Log.Error(e, $"Restoring variant {entry.Key} failed.");
                }
            }
            Mod.Log.Info($"Restored {restored} variants (+{extensions} upgrade extensions), {failed} failed.");
        }

        private int RestoreExtensions(ManifestEntry entry, ObjectGeometryPrefab ownerBase, DeformSpec spec,
            string ownerVariantName)
        {
            int restored = 0;
            foreach (string key in entry.Extensions)
            {
                int slash = key.IndexOf('/');
                if (slash > 0 && _resolver.TryFind(key.Substring(0, slash), key.Substring(slash + 1), out var found) &&
                    found is BuildingExtensionPrefab extensionBase &&
                    TryGetOrCreateExtensionVariant(ownerBase, spec, ownerVariantName, extensionBase, out _))
                    restored++;
                else
                    Mod.Log.Warn($"Restoring upgrade extension {key} of {entry.Key} failed.");
            }
            return restored;
        }

        private HashSet<string> CollectUsedVariantKeys()
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var seenPrefabs = new HashSet<Entity>();
            using var prefabRefs = _buildingQuery.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            foreach (var prefabRef in prefabRefs)
            {
                if (!seenPrefabs.Add(prefabRef.m_Prefab) || !_prefabSystem.TryGetPrefab(prefabRef, out PrefabBase prefab))
                    continue;
                if (VariantNaming.IsVariantName(prefab.name))
                    keys.Add(ManifestEntry.MakeKey(prefab.GetType().Name, prefab.name));
            }
            return keys;
        }

        private void SaveIfDirty()
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                if (_manifest.IsDirty)
                    _manifest.Save(ModPaths.ManifestFile);
                if (_renderBuilder.BoundsCache.IsDirty)
                    _renderBuilder.BoundsCache.Save(ModPaths.BoundsCacheFile);
                if (stopwatch.ElapsedMilliseconds >= 1)
                    Mod.Log.Info($"Saved manifest and bounds cache: {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Saving variant manifest failed.");
            }
        }

        private static void PreserveCorruptManifest()
        {
            try
            {
                string target = ModPaths.ManifestFile + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                if (File.Exists(ModPaths.ManifestFile))
                    File.Copy(ModPaths.ManifestFile, target);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not preserve corrupt manifest.");
            }
        }

        private readonly struct PendingSwap
        {
            public PendingSwap(Entity targetPrefab, ObjectGeometryPrefab basePrefab, DeformSpec spec,
                List<(Entity Extension, Entity Target)> extensions)
            {
                TargetPrefab = targetPrefab;
                BasePrefab = basePrefab;
                Spec = spec;
                Extensions = extensions;
            }

            public Entity TargetPrefab { get; }
            public ObjectGeometryPrefab BasePrefab { get; }
            public DeformSpec Spec { get; }
            public List<(Entity Extension, Entity Target)> Extensions { get; }
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
