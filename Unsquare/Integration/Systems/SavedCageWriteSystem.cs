using System;
using System.Collections.Generic;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Integration.Saves;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Runs in the save frame before the game picks the prefabs to save: writes each deformed building's cage into
    /// <see cref="SavedCage"/> and points it (and its extensions) at the base, so saves never reference variants.
    /// <see cref="SavedCageRestoreSystem"/> puts them back after the serializer in the same frame.
    /// </summary>
    /// <remarks>No Updated and no lot sync: the simulation never sees the base, and the lot keeps its layout.</remarks>
    public partial class SavedCageWriteSystem : GameSystemBase
    {
        private readonly Dictionary<Entity, (bool Saved, bool IsVariant, Entity Base, DeformSpec Spec)> _lookups = new();
        private readonly List<(Entity Building, Entity Variant, Entity Base, DeformSpec Spec)> _toSave = new();
        private readonly HashSet<Entity> _saving = new();
        private readonly List<int> _scratch = new();

        private VariantSystem _variants;
        private EntityQuery _buildings;
        private EntityQuery _extensions;
        private EntityQuery _savedCages;

        protected override void OnCreate()
        {
            base.OnCreate();
            _variants = World.GetOrCreateSystemManaged<VariantSystem>();
            _buildings = GetEntityQuery(ComponentType.ReadOnly<Building>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
            // Stand-alone upgrade buildings are Extensions too, but have their own cage: they are buildings here.
            _extensions = GetEntityQuery(ComponentType.ReadOnly<Extension>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Building>(), ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
            _savedCages = GetEntityQuery(ComponentType.ReadOnly<SavedCage>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Temp>(), ComponentType.Exclude<Deleted>());
        }

        protected override void OnUpdate()
        {
            var ledger = _variants.SaveSwaps;
            ledger.RestoreAll(EntityManager); // a previous save that never reached its restore
            try
            {
                Write(ledger);
            }
            catch (Exception e)
            {
                // Back to today's save: buildings on their variants (left-over cages are harmless).
                Mod.Log.Error(e, "Saving deformed buildings as base + cage failed; they are saved as variants.");
                ledger.RestoreAll(EntityManager);
            }
            finally
            {
                _lookups.Clear();
                _toSave.Clear();
                _saving.Clear();
            }
            if (ledger.Count > 0)
                _variants.Enabled = true; // restores on its next update if the save throws before our restore
        }

        private void Write(SaveSwapLedger ledger)
        {
            int left = CollectBuildings();
            int stale = RemoveStaleCages();

            using (var missing = new NativeList<Entity>(_toSave.Count, Allocator.Temp))
            {
                foreach (var item in _toSave)
                {
                    if (!EntityManager.HasComponent<SavedCage>(item.Building))
                        missing.Add(item.Building);
                }
                if (missing.Length > 0)
                    EntityManager.AddComponent(missing.AsArray(), ComponentType.ReadWrite<SavedCage>());
            }
            foreach (var item in _toSave)
            {
                SavedCages.TryFrom(item.Spec, _scratch, out var cage); // checked when collected
                EntityManager.SetComponentData(item.Building, cage);
                ledger.Swap(EntityManager, item.Building, item.Variant, item.Base);
            }

            int extensions = 0;
            using var entities = _extensions.ToEntityArray(Allocator.Temp);
            foreach (var extension in entities)
            {
                // Read live: the building pass may have moved it already.
                var prefab = EntityManager.GetComponentData<PrefabRef>(extension).m_Prefab;
                if (ledger.Contains(extension) || !_variants.TryGetSavedExtensionBase(prefab, out var baseEntity))
                    continue;
                ledger.Swap(EntityManager, extension, prefab, baseEntity);
                extensions++;
            }
            Mod.Log.Info($"Save: {_toSave.Count} deformed buildings as base + cage, {extensions} extensions on their " +
                         $"base, {left} left on variants, {stale} old cages removed.");
        }

        // Deformed buildings whose base the save can hold; returns how many stay on their variant.
        private int CollectBuildings()
        {
            int left = 0;
            using var entities = _buildings.ToEntityArray(Allocator.Temp);
            using var prefabRefs = _buildings.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var prefab = prefabRefs[i].m_Prefab;
                if (!_lookups.TryGetValue(prefab, out var lookup))
                {
                    bool saved = _variants.TryGetSavedSpec(prefab, out bool isVariant, out var baseEntity, out var spec);
                    lookup = (saved, isVariant, baseEntity, spec);
                    _lookups[prefab] = lookup;
                }
                if (lookup.Saved && SavedCages.TryFrom(lookup.Spec, _scratch, out _))
                {
                    _toSave.Add((entities[i], prefab, lookup.Base, lookup.Spec));
                    _saving.Add(entities[i]);
                }
                else if (lookup.IsVariant)
                    left++;
            }
            return left;
        }

        // Cages of buildings no longer deformed (reset, or left on a variant this time) would come back on load.
        private int RemoveStaleCages()
        {
            using var entities = _savedCages.ToEntityArray(Allocator.Temp);
            using var stale = new NativeList<Entity>(entities.Length, Allocator.Temp);
            foreach (var entity in entities)
            {
                if (!_saving.Contains(entity))
                    stale.Add(entity);
            }
            if (stale.Length > 0)
                EntityManager.RemoveComponent(stale.AsArray(), ComponentType.ReadWrite<SavedCage>());
            return stale.Length;
        }
    }
}
