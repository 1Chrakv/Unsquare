using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.City;
using Game.Common;
using Game.Prefabs;
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
        private readonly List<Entity> _extensions = new();
        private readonly HashSet<Entity> _upgradesLogged = new();
        private readonly Debouncer<Entity, DeformSpec> _debouncer = new(PipelineOptions.Default.RebakeDelaySeconds);

        private PrefabSystem _prefabSystem;
        private CityConfigurationSystem _citySystem;
        private PrefabResolver _resolver;
        private GeometryAssetStore _store;
        private RenderVariantBuilder _renderBuilder;
        private VariantPrefabFactory _factory;
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
            _store = new GeometryAssetStore();
            _renderBuilder = new RenderVariantBuilder(_store);
            _factory = new VariantPrefabFactory(_prefabSystem, _renderBuilder,
                new IAttachmentDeformer[]
                {
                    new SubObjectDeformer(), new SubAreaDeformer(), new SubLaneDeformer(), new SubNetDeformer(),
                });
            _buildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            Enabled = false; // woken by Request
        }

        protected override void OnUpdate()
        {
            _due.Clear();
            _debouncer.CollectDue(UnityEngine.Time.realtimeSinceStartupAsDouble, _due);
            foreach (var request in _due)
                Bake(request.Key, request.Value);

            ProcessSimData();
            ProcessSwaps();
            SaveIfDirty();
            Enabled = _debouncer.Count > 0 || _swaps.Count > 0 || _simDataPending.Count > 0;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _debouncer.Clear();
            _swaps.Clear();
            _statuses.Clear();
            _simDataPending.Clear();
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
            spec ??= DeformSpec.Identity;
            if (spec.Offsets.IsIdentity)
                spec = DeformSpec.Identity; // the base prefab has no terrain flag
            _swaps.Remove(building);

            if (spec.Equals(description.Applied))
            {
                _debouncer.Cancel(building);
                SetStatus(building, VariantStatus.Idle);
                return;
            }
            if (spec.Offsets.IsIdentity && _prefabSystem.TryGetEntity(description.BasePrefab, out var baseEntity))
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
            if (!TryGetBoxes(basePrefab, out var lotBox, out _))
                return null;
            IDeformation from = null;
            if (VariantNaming.IsVariantName(current.name))
            {
                if (!_resolver.TryGetVariant(current.name, out var info))
                    return null;
                from = Deformation(lotBox, info.Spec);
            }
            return new LotRemap(from, Deformation(lotBox, target));
        }

        private static IDeformation Deformation(CageBox lotBox, DeformSpec spec) =>
            spec == null || spec.Offsets.IsIdentity ? null : new TrilinearCageDeformation(lotBox, spec.Offsets);

        private bool TryDescribe(Entity building, out BuildingDescription description)
        {
            description = default;
            if (!EntityManager.HasComponent<Building>(building) ||
                !_resolver.TryGetPrefab(EntityManager, building, out var current) ||
                !_resolver.TryResolveBase(current, out var basePrefab, out var applied) ||
                !TryGetBoxes(basePrefab, out var lotBox, out var buildingBox))
                return false;
            description = new BuildingDescription(basePrefab, lotBox, buildingBox, applied);
            return true;
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
            bool created = TryGetOrCreateVariant(description.BasePrefab, spec, isRestore: false, out var variant);
            stopwatch.Stop();
            // 0 new assets means everything was reused (existing prefab or cached geometry).
            Mod.Log.Info($"Bake for building {building}: {(created ? variant.name : "failed")}, " +
                         $"{stopwatch.ElapsedMilliseconds} ms, {_renderBuilder.CreatedAssetCount - createdBefore} new assets");

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
            var offsets = spec.Offsets;
            string typeName = basePrefab.GetType().Name;
            string variantName = VariantNaming.VariantName(basePrefab.name, offsets, spec.FlatTerrain);
            if (_resolver.TryFind(typeName, variantName, out var existing))
            {
                variant = existing as ObjectGeometryPrefab;
                if (variant != null)
                    _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, spec));
                return variant != null;
            }
            if (!TryGetBoxes(basePrefab, out var box, out var buildingBox))
            {
                Mod.Log.Warn($"No lot cage box for {basePrefab.name}.");
                return false;
            }

            // Restores skip validation: a save already references the variant, refusing it breaks the building.
            if (!isRestore && CageValidator.Validate(box, offsets, _options.Limits) != CageValidity.Valid)
            {
                Mod.Log.Warn($"Rejected invalid cage for {basePrefab.name}: {offsets}");
                return false;
            }

            var assets = new HashSet<string>(StringComparer.Ordinal);
            if (!_factory.TryCreate(basePrefab, variantName, box, buildingBox, float3.zero, offsets,
                    _options.Attachments, _options.CoarseLodTwistMeters, assets, out variant))
                return false;
            _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, spec));
            // Restored variants are never swapped to, so the copy cannot wait for TrySwap.
            if (_prefabSystem.TryGetEntity(variant, out var variantEntity))
                _simDataPending[variantEntity] = basePrefab;

            string key = ManifestEntry.MakeKey(typeName, variantName);
            if (isRestore)
            {
                _manifest.MergeAssets(key, assets);
                return true;
            }
            _manifest.Register(new ManifestEntry
            {
                BaseType = typeName,
                BaseName = basePrefab.name,
                VariantName = variantName,
                Offsets = offsets.ToArray(),
                FlatTerrain = spec.FlatTerrain,
                Assets = new List<string>(assets),
            }, _citySystem.cityName, DateTime.UtcNow);
            return true;
        }

        // Base targets when ownerVariantName is null; an extension that can't be deformed goes back to its base.
        private List<(Entity Extension, Entity Target)> PlanExtensionTargets(Entity building,
            ObjectGeometryPrefab ownerBase, DeformSpec spec, string ownerVariantName)
        {
            var targets = new List<(Entity, Entity)>();
            InstalledExtensions.Collect(EntityManager, building, _extensions);
            foreach (var extension in _extensions)
            {
                if (!_resolver.TryGetPrefab(EntityManager, extension, out var current) ||
                    !TryResolveExtensionBase(current, out var extensionBase))
                    continue;
                ObjectGeometryPrefab target = extensionBase;
                if (ownerVariantName != null && IsDeformable(extensionBase))
                {
                    if (!InstalledExtensions.IsAtPrefabPlacement(EntityManager, building, extension,
                            extensionBase.m_Position, out var offset))
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

        private bool TryResolveExtensionBase(PrefabBase current, out BuildingExtensionPrefab extensionBase)
        {
            extensionBase = null;
            if (!VariantNaming.TryParse(current.name, out string baseName))
                extensionBase = current as BuildingExtensionPrefab;
            else if (_resolver.TryFind(current.GetType().Name, baseName, out var found))
                extensionBase = found as BuildingExtensionPrefab;
            return extensionBase != null;
        }

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
                return variant != null;
            }
            if (!TryGetBoxes(ownerBase, out var box, out var buildingBox))
                return false;

            var assets = new HashSet<string>(StringComparer.Ordinal);
            if (!_factory.TryCreate(extensionBase, variantName, box, buildingBox, extensionBase.m_Position,
                    spec.Offsets, _options.Attachments, _options.CoarseLodTwistMeters, assets, out variant))
                return false;
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
                    else if (TryGetBoxes(ownerBase, out var lotBox, out _))
                        remap = new LotRemap(null, Deformation(lotBox, spec));
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

            int copied = 0;
            foreach (var variantEntity in _readyPrefabs)
            {
                var basePrefab = _simDataPending[variantEntity];
                _simDataPending.Remove(variantEntity);
                try
                {
                    if (EntityManager.Exists(variantEntity) && _prefabSystem.TryGetEntity(basePrefab, out var donor))
                    {
                        SimDataCopier.CopyMissing(EntityManager, donor, variantEntity);
                        UpgradeRegistration.RemoveFromBuildings(EntityManager, variantEntity);
                        copied++;
                    }
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, $"Copying sim data from {basePrefab.name} to variant {variantEntity} failed.");
                }
            }
            if (copied > 0)
                Mod.Log.Info($"Copied sim data to {copied} variants, {_simDataPending.Count} still initializing.");
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
                if (_prefabSystem.TryGetEntity(swap.BasePrefab, out var donor) && donor != swap.TargetPrefab)
                    SimDataCopier.CopyMissing(EntityManager, donor, swap.TargetPrefab);
                var netRemap = _resolver.TryGetPrefab(EntityManager, building, out var currentPrefab)
                    ? BuildNetRemap(currentPrefab, swap.BasePrefab, swap.Spec)
                    : null;
                LogUpgradesOnce(building);
                var extensions = BuildExtensionSwaps(swap.Extensions, netRemap, swap.BasePrefab, swap.Spec);
                PrefabSwapper.Swap(EntityManager, building, swap.TargetPrefab, extensions, _options.AreaMatchMeters,
                    netRemap);
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
                    var spec = new DeformSpec(CageOffsets.FromArray(entry.Offsets), entry.FlatTerrain);
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
                if (_manifest.IsDirty)
                    _manifest.Save(ModPaths.ManifestFile);
                if (_renderBuilder.BoundsCache.IsDirty)
                    _renderBuilder.BoundsCache.Save(ModPaths.BoundsCacheFile);
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
    }
}
