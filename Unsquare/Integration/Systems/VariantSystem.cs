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
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;
using Unsquare.Core.Scheduling;
using Unsquare.Integration.Assets;
using Unsquare.Integration.Entities;
using Unsquare.Integration.Prefabs;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Orchestrates debounce → bake → wait for prefab init → sim copy → swap, plus restore and housekeeping.
    /// Runs in PrefabUpdate before PrefabInitializeSystem and disables itself while idle.
    /// </summary>
    public partial class VariantSystem : GameSystemBase, IVariantPipeline
    {
        private const double DebounceSeconds = 0.4;

        private readonly Dictionary<Entity, PendingSwap> _swaps = new();
        private readonly Dictionary<Entity, VariantStatus> _statuses = new();
        private readonly Dictionary<ObjectGeometryPrefab, CageBox> _boxes = new();
        private readonly List<KeyValuePair<Entity, CageOffsets>> _due = new();
        private readonly List<(Entity Building, bool Succeeded)> _finished = new();
        private readonly Debouncer<Entity, CageOffsets> _debouncer = new(DebounceSeconds);

        private PrefabSystem _prefabSystem;
        private CityConfigurationSystem _citySystem;
        private PrefabResolver _resolver;
        private GeometryAssetStore _store;
        private RenderVariantBuilder _renderBuilder;
        private VariantPrefabFactory _factory;
        private VariantManifest _manifest = new();
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

        protected override void OnCreate()
        {
            base.OnCreate();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _citySystem = World.GetOrCreateSystemManaged<CityConfigurationSystem>();
            _resolver = new PrefabResolver(_prefabSystem);
            _store = new GeometryAssetStore();
            _renderBuilder = new RenderVariantBuilder(_store);
            _factory = new VariantPrefabFactory(_prefabSystem, _renderBuilder);
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

            ProcessSwaps();
            SaveIfDirty();
            Enabled = _debouncer.Count > 0 || _swaps.Count > 0;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _debouncer.Clear();
            _swaps.Clear();
            _statuses.Clear();
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
            if (mode == GameMode.Game && !string.IsNullOrEmpty(_citySystem.cityName))
            {
                _manifest.MarkUsage(_citySystem.cityName, CollectUsedVariantKeys(), DateTime.UtcNow);
                SaveIfDirty();
            }
        }

        bool IVariantPipeline.TryDescribe(Entity building, out BuildingDescription description) =>
            TryDescribe(building, out description);

        bool IVariantPipeline.TryGetRequested(Entity building, out CageOffsets offsets)
        {
            if (_debouncer.TryPeek(building, out offsets))
                return true;
            bool swapping = _swaps.TryGetValue(building, out var swap);
            offsets = swapping ? swap.Offsets : null;
            return swapping;
        }

        VariantStatus IVariantPipeline.GetStatus(Entity building) =>
            _statuses.TryGetValue(building, out var status) ? status : VariantStatus.Idle;

        void IVariantPipeline.Request(Entity building, CageOffsets offsets)
        {
            if (!TryDescribe(building, out var description))
                return;
            _swaps.Remove(building);

            if (offsets.Equals(description.AppliedOffsets))
            {
                _debouncer.Cancel(building);
                SetStatus(building, VariantStatus.Idle);
                return;
            }
            if (offsets.IsIdentity && _prefabSystem.TryGetEntity(description.BasePrefab, out var baseEntity))
            {
                _debouncer.Cancel(building); // reverting needs no bake
                QueueSwap(building, baseEntity, description.BasePrefab, offsets);
            }
            else
            {
                _debouncer.Request(building, offsets, UnityEngine.Time.realtimeSinceStartupAsDouble);
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
                if (PrefabSwapper.Swap(EntityManager, building, baseEntity))
                {
                    count++;
                    _buildingChanged?.Invoke(building);
                }
            }
            Mod.Log.Info($"Reset {count} deformed buildings.");
            return count;
        }

        private bool TryDescribe(Entity building, out BuildingDescription description)
        {
            description = default;
            if (!EntityManager.HasComponent<Building>(building) ||
                !_resolver.TryGetPrefab(EntityManager, building, out var current) ||
                !_resolver.TryResolveBase(current, out var basePrefab, out var applied) ||
                !TryGetBox(basePrefab, out var box))
                return false;
            description = new BuildingDescription(basePrefab, box, applied);
            return true;
        }

        private bool TryGetBox(ObjectGeometryPrefab basePrefab, out CageBox box)
        {
            if (_boxes.TryGetValue(basePrefab, out box))
                return true;
            if (!CageBoxMeasurer.TryMeasure(basePrefab, out box))
                return false;
            _boxes[basePrefab] = box;
            return true;
        }

        private void Bake(Entity building, CageOffsets offsets)
        {
            if (!TryDescribe(building, out var description))
            {
                SetStatus(building, VariantStatus.Idle);
                return;
            }
            var stopwatch = Stopwatch.StartNew();
            int createdBefore = _renderBuilder.CreatedAssetCount;
            bool created = TryGetOrCreateVariant(description.BasePrefab, description.Box, offsets, isRestore: false,
                out var variant);
            stopwatch.Stop();
            // 0 new assets means everything was reused (existing prefab or cached geometry).
            Mod.Log.Info($"Bake for building {building}: {(created ? variant.name : "failed")}, " +
                         $"{stopwatch.ElapsedMilliseconds} ms, {_renderBuilder.CreatedAssetCount - createdBefore} new assets");

            if (created && _prefabSystem.TryGetEntity(variant, out var variantEntity))
                QueueSwap(building, variantEntity, description.BasePrefab, offsets);
            else
                SetStatus(building, VariantStatus.Failed);
        }

        private bool TryGetOrCreateVariant(ObjectGeometryPrefab basePrefab, CageBox box, CageOffsets offsets,
            bool isRestore, out ObjectGeometryPrefab variant)
        {
            string typeName = basePrefab.GetType().Name;
            string variantName = VariantNaming.VariantName(basePrefab.name, offsets);
            if (_resolver.TryFind(typeName, variantName, out var existing))
            {
                variant = existing as ObjectGeometryPrefab;
                if (variant != null)
                    _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, offsets));
                return variant != null;
            }

            // Restores skip validation: a save already references the variant, refusing it breaks the building.
            if (!isRestore && CageValidator.Validate(box, offsets) != CageValidity.Valid)
            {
                Mod.Log.Warn($"Rejected invalid cage for {basePrefab.name}: {offsets}");
                variant = null;
                return false;
            }

            var assets = new HashSet<string>(StringComparer.Ordinal);
            if (!_factory.TryCreate(basePrefab, variantName, box, offsets, assets, out variant))
                return false;
            _resolver.RememberVariant(variantName, new VariantInfo(basePrefab, offsets));

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
                Assets = new List<string>(assets),
            }, _citySystem.cityName, DateTime.UtcNow);
            return true;
        }

        private void QueueSwap(Entity building, Entity targetPrefab, ObjectGeometryPrefab basePrefab, CageOffsets offsets)
        {
            _swaps[building] = new PendingSwap(targetPrefab, basePrefab, offsets);
            SetStatus(building, VariantStatus.WaitingForPrefab);
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
                if (!_simulationReady || EntityManager.HasComponent<Created>(swap.TargetPrefab))
                    continue;
                _finished.Add((building, TrySwap(building, swap)));
            }

            foreach (var (building, succeeded) in _finished)
            {
                _swaps.Remove(building);
                SetStatus(building, succeeded ? VariantStatus.Idle : VariantStatus.Failed);
            }
        }

        private bool TrySwap(Entity building, PendingSwap swap)
        {
            try
            {
                if (_prefabSystem.TryGetEntity(swap.BasePrefab, out var donor) && donor != swap.TargetPrefab)
                    SimDataCopier.CopyMissing(EntityManager, donor, swap.TargetPrefab);
                PrefabSwapper.Swap(EntityManager, building, swap.TargetPrefab);
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Swapping prefab of building {building} failed.");
                return false;
            }
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
                new VariantHousekeeper(_store).Run(_manifest, _renderBuilder.BoundsCache, DateTime.UtcNow);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Housekeeping failed.");
            }
        }

        private void RestoreVariants()
        {
            int restored = 0, failed = 0;
            foreach (var entry in new List<ManifestEntry>(_manifest.Entries))
            {
                try
                {
                    if (_resolver.TryFind(entry.BaseType, entry.BaseName, out var found) &&
                        found is ObjectGeometryPrefab basePrefab && TryGetBox(basePrefab, out var box) &&
                        TryGetOrCreateVariant(basePrefab, box, CageOffsets.FromArray(entry.Offsets), isRestore: true, out _))
                        restored++;
                    else
                        failed++;
                }
                catch (Exception e)
                {
                    failed++;
                    Mod.Log.Error(e, $"Restoring variant {entry.Key} failed.");
                }
            }
            Mod.Log.Info($"Restored {restored} variants, {failed} failed.");
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
            public PendingSwap(Entity targetPrefab, ObjectGeometryPrefab basePrefab, CageOffsets offsets)
            {
                TargetPrefab = targetPrefab;
                BasePrefab = basePrefab;
                Offsets = offsets;
            }

            public Entity TargetPrefab { get; }
            public ObjectGeometryPrefab BasePrefab { get; }
            public CageOffsets Offsets { get; }
        }
    }
}
