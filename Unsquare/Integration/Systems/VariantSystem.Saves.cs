using System;
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;
using Unsquare.Integration.Saves;

namespace Unsquare.Integration.Systems
{
    // Saves: saved cages written and restored around saves and loads, and the legacy manifest of older saves.
    public partial class VariantSystem
    {
        private readonly HashSet<Entity> _unsavableLogged = new();
        private readonly List<int> _savedScratch = new();
        private readonly LegacyManifestStore _legacy = new();
        private EntityQuery _savedCageQuery;
        private bool _savedCagesPending;
        private Action<int> _legacySaveLoaded;

        /// <summary>Buildings on their base for the save in progress; see <see cref="SavedCageWriteSystem"/>.</summary>
        internal SaveSwapLedger SaveSwaps { get; } = new();

        /// <summary>Prefab entities of every variant created or reused this session.</summary>
        internal IReadOnlyCollection<Entity> VariantPrefabs => _variantPrefabs;

        event Action<int> IVariantPipeline.LegacySaveLoaded
        {
            add => _legacySaveLoaded += value;
            remove => _legacySaveLoaded -= value;
        }

        // Prefabs added here lose Created before PrefabInitializeSystem sees them and stay uninitialized (no instance
        // archetype; swapping to them crashes natively), so the variants are created in the next prefab update.
        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            _savedCagesPending = true;
            Enabled = true;
        }

        private void RestoreSavedCagesSafely()
        {
            try
            {
                RestoreSavedCages();
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Restoring deformed buildings from the save failed.");
            }
            _sourceMeshes.ReleaseAll(); // rebakes of evicted shapes; nobody edits most of them now
        }

        private void AnnounceLegacySave()
        {
            try
            {
                int legacy = CountLegacyBuildings();
                if (legacy == 0)
                    return;
                Mod.Log.Info($"{legacy} deformed buildings were loaded in the old save format; saving converts them.");
                _legacySaveLoaded?.Invoke(legacy);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Checking the save format of deformed buildings failed.");
            }
        }

        // Saved cages are still on their bases here, so a building on a variant was loaded from an old save.
        private int CountLegacyBuildings()
        {
            int count = 0;
            var convertible = new Dictionary<Entity, bool>();
            using var prefabRefs = _buildingQuery.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            foreach (var prefabRef in prefabRefs)
            {
                if (!convertible.TryGetValue(prefabRef.m_Prefab, out bool converts))
                {
                    converts = TryGetSavedSpec(prefabRef.m_Prefab, out _, out _, out _);
                    convertible[prefabRef.m_Prefab] = converts;
                }
                if (converts)
                    count++;
            }
            return count;
        }

        // The save's base was replaced before its shape came back: a finished level-up carries the cage over, anything
        // else leaves the building as it is (swapping would undo the change).
        private bool HandOverLoadSwap(Entity building, PendingSwap swap, Entity current)
        {
            if (_prefabSystem.TryGetPrefab(current, out PrefabBase prefab) && prefab is ObjectGeometryPrefab &&
                !VariantNaming.IsVariantName(prefab.name) &&
                LevelUpCarrySystem.SameLot(EntityManager, swap.ExpectedCurrent, current) &&
                TryGetBoxes(swap.BasePrefab, out var fromLot, out _))
            {
                Mod.Log.Info($"Building {building} levelled up before its saved shape was restored: carrying it over.");
                PrepareLevelUp(building, swap.ExpectedCurrent, current, fromLot, swap.Spec);
                return true;
            }
            Mod.Log.Warn($"Building {building} changed prefab before its saved shape was restored; it stays as it is.");
            return true;
        }

        private bool LoadState()
        {
            bool trusted = _legacy.Load();
            BoundsCache.Load(ModPaths.BoundsCacheFile, out var bounds);
            _renderBuilder.BoundsCache = bounds;
            return trusted;
        }

        private void RunHousekeeping()
        {
            try
            {
                new VariantHousekeeper(_store).Run(_legacy.Manifest, _renderBuilder.BoundsCache, DateTime.UtcNow,
                    _options.GeometryCacheBytes);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Housekeeping failed.");
            }
        }

        bool IVariantPipeline.TryRetireLegacyManifest(out int retired, out string refusal)
        {
            if (!_stateLoaded)
            {
                retired = 0;
                refusal = "not ready yet";
                return false;
            }
            return _legacy.TryRetire(_simulationReady ? _citySystem.cityName : null, out retired, out refusal);
        }

        bool IVariantPipeline.TryRestoreLegacyShapes(out int restored, out string refusal)
        {
            if (!_stateLoaded)
            {
                restored = 0;
                refusal = "not ready yet";
                return false;
            }
            if (!_legacy.TryRestore(out restored, out refusal))
                return false;
            SaveIfDirty();
            return true;
        }

        private void RestoreVariants()
        {
            _legacy.ClearFailures();
            int restored = 0, failed = 0, extensions = 0;
            foreach (var entry in new List<ManifestEntry>(_legacy.Manifest.Entries))
            {
                try
                {
                    var spec = entry.ToSpec();
                    if (_resolver.TryFind(entry.BaseType, entry.BaseName, out var found) &&
                        found is ObjectGeometryPrefab basePrefab &&
                        TryGetOrCreateVariant(basePrefab, spec, VariantOrigin.Legacy, out var variant))
                    {
                        restored++;
                        extensions += RestoreExtensions(entry, basePrefab, spec, variant.name);
                    }
                    else
                    {
                        failed++;
                        _legacy.RecordFailure(entry);
                        Mod.Log.Warn($"Restoring variant {entry.Key} failed: base missing or variant not created.");
                    }
                }
                catch (Exception e)
                {
                    failed++;
                    _legacy.RecordFailure(entry);
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
                    TryGetOrCreateExtensionVariant(ownerBase, spec, ownerVariantName, extensionBase,
                        VariantOrigin.Legacy, out _))
                    restored++;
                else
                    Mod.Log.Warn($"Restoring upgrade extension {key} of {entry.Key} failed.");
            }
            return restored;
        }

        // Buildings the save holds as base + cage: create each variant once and swap them without moving their lot
        // items, which the save already has in the variant's layout.
        private void RestoreSavedCages()
        {
            if (_savedCageQuery.IsEmptyIgnoreFilter)
                return;
            var stopwatch = Stopwatch.StartNew();
            int createdBefore = _renderBuilder.CreatedAssetCount;
            var variants = new Dictionary<(ObjectGeometryPrefab, DeformSpec), (Entity Entity, string Name)>();
            int queued = 0, unreadable = 0, failed = 0;
            using var buildings = _savedCageQuery.ToEntityArray(Allocator.Temp);
            foreach (var building in buildings)
            {
                if (!SavedCages.TryReadBuilding(EntityManager, _prefabSystem, building, _savedScratch,
                        out var basePrefab, out var baseEntity, out var spec))
                {
                    unreadable++;
                    continue;
                }
                if (!variants.TryGetValue((basePrefab, spec), out var variant))
                {
                    variant = TryGetOrCreateVariant(basePrefab, spec, VariantOrigin.Load, out var created) &&
                              _prefabSystem.TryGetEntity(created, out var createdEntity)
                        ? (createdEntity, created.name)
                        : (Entity.Null, null);
                    variants[(basePrefab, spec)] = variant;
                    if (variant.Entity == Entity.Null)
                        Mod.Log.Warn($"No variant of {basePrefab.name} for the saved cage {spec}; its buildings stay undeformed.");
                }
                if (variant.Entity == Entity.Null)
                {
                    failed++;
                    continue;
                }
                QueueSwap(building, variant.Entity, basePrefab, spec,
                    PlanExtensionTargets(building, basePrefab, spec, variant.Name, VariantOrigin.Load), syncLot: false,
                    expectedCurrent: baseEntity);
                queued++;
            }
            stopwatch.Stop();
            Mod.Log.Info($"Saved cages: {queued} buildings on {variants.Count} variants " +
                         $"({_renderBuilder.CreatedAssetCount - createdBefore} new assets, {stopwatch.ElapsedMilliseconds} ms), " +
                         $"{failed} failed, {unreadable} not restored (plain, on a variant or unreadable).");
            if (queued > 0)
                Enabled = true;
        }

        /// <summary>
        /// Base and cage a building on <paramref name="prefab"/> is saved as. False for plain prefabs, and for variants
        /// whose base would not exist at load (another mod's runtime prefab) or whose cage is unknown.
        /// </summary>
        internal bool TryGetSavedSpec(Entity prefab, out bool isVariant, out Entity baseEntity, out DeformSpec spec)
        {
            isVariant = false;
            baseEntity = Entity.Null;
            spec = null;
            if (!_prefabSystem.TryGetPrefab(prefab, out PrefabBase current) || current == null ||
                !VariantNaming.IsVariantName(current.name))
                return false;
            isVariant = true;
            if (!_resolver.TryGetVariant(current.name, out var info) || VariantNaming.IsVariantName(info.BasePrefab.name) ||
                _foreign.IsRuntimePrefab(info.BasePrefab) || !_prefabSystem.TryGetEntity(info.BasePrefab, out baseEntity))
            {
                if (!_unsavableLogged.Add(prefab))
                    return false;
                // Only the legacy restore brings such a variant back at load; without an entry the save loses it.
                if (_legacy.Manifest.TryGet(ManifestEntry.MakeKey(current.GetType().Name, current.name), out _))
                    Mod.Log.Info($"{current.name} is saved as the variant itself: its base is unknown or not saved by the game.");
                else
                    Mod.Log.Warn($"{current.name} is saved as the variant itself but has no manifest entry: " +
                                 "its buildings will be missing when this save is loaded.");
                return false;
            }
            spec = info.Spec;
            return true;
        }

        /// <summary>The plain extension prefab an extension variant prefab was baked from.</summary>
        internal bool TryGetSavedExtensionBase(Entity prefab, out Entity baseEntity)
        {
            baseEntity = Entity.Null;
            return _prefabSystem.TryGetPrefab(prefab, out PrefabBase current) && current != null &&
                   VariantNaming.TryParse(current.name, out string baseName) &&
                   _resolver.TryFind(current.GetType().Name, baseName, out var found) &&
                   found is BuildingExtensionPrefab && _prefabSystem.TryGetEntity(found, out baseEntity);
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
            // Saved cages are still on their base here, so a converted city drops out of its legacy entries.
            return keys;
        }
    }
}
