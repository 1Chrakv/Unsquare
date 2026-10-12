using System;
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Serialization.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Terrain;
using Unsquare.Integration.Entities;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Shapes the terrain of deformed lots: collapses and raises the variant prefab's pad (<see cref="TerrainSlope"/>)
    /// and adds the edge lift to the lot heights right after LotHeightSystem wrote them.
    /// Nothing is saved; prefab data and lot heights are rebuilt and lifted again after load.
    /// </summary>
    public partial class LotLiftSystem : GameSystemBase
    {
        private readonly Dictionary<Entity, Applied> _applied = new();
        private readonly Dictionary<Entity, TerrainSlope> _slopes = new();
        private readonly HashSet<Entity> _changedPrefabs = new();
        private readonly List<Entity> _tracked = new();
        private readonly TerrainSlopeApplier _applier = new();

        private VariantSystem _variants;
        private Game.Objects.UpdateCollectSystem _objectUpdates;
        private Game.Net.UpdateCollectSystem _netUpdates;
        private ModificationBarrier5 _barrier;
        private EntityQuery _updatedLots;
        private EntityQuery _allLots;
        private EntityCommandBuffer _commands;
        private bool _hasCommands;
        private bool _loaded;

        protected override void OnCreate()
        {
            base.OnCreate();
            _variants = World.GetOrCreateSystemManaged<VariantSystem>();
            _objectUpdates = World.GetOrCreateSystemManaged<Game.Objects.UpdateCollectSystem>();
            _netUpdates = World.GetOrCreateSystemManaged<Game.Net.UpdateCollectSystem>();
            _barrier = World.GetOrCreateSystemManaged<ModificationBarrier5>();
            _updatedLots = GetEntityQuery(ComponentType.ReadOnly<Lot>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.ReadOnly<Updated>(), ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
            _allLots = GetEntityQuery(ComponentType.ReadOnly<Lot>(), ComponentType.ReadOnly<PrefabRef>(),
                ComponentType.Exclude<Deleted>(), ComponentType.Exclude<Temp>());
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            // Variant prefab entities are created anew while loading; forget the old ones without reverting.
            _applied.Clear();
            _slopes.Clear();
            _applier.Clear();
        }

        // LotHeightSystem recomputes every lot in its first update after a load; lift them in the same frame.
        protected override void OnGameLoaded(Context serializationContext)
        {
            base.OnGameLoaded(serializationContext);
            _loaded = true;
        }

        protected override void OnUpdate()
        {
            try
            {
                Lift();
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Lifting lot terrain failed.");
                _applied.Clear();
            }
        }

        private void Lift()
        {
            _hasCommands = false;
            _changedPrefabs.Clear();
            if (_loaded)
            {
                ProcessQuery(_allLots, markUpdated: false);
                _loaded = false;
                return;
            }

            bool anyUpdated = !_updatedLots.IsEmptyIgnoreFilter;
            if (anyUpdated)
                ProcessQuery(_updatedLots, markUpdated: true);
            // Same triggers as LotHeightSystem: it may have rewritten a lifted lot next to a changed object or net.
            if (_applied.Count > 0 && (anyUpdated || _objectUpdates.isUpdated || _netUpdates.netsUpdated))
            {
                _tracked.Clear();
                _tracked.AddRange(_applied.Keys);
                foreach (var entity in _tracked)
                {
                    if (EntityManager.Exists(entity) && !EntityManager.HasComponent<Deleted>(entity) &&
                        EntityManager.TryGetComponent(entity, out PrefabRef prefabRef))
                        Process(entity, LiftFor(prefabRef.m_Prefab), markUpdated: true);
                    else
                        _applied.Remove(entity);
                }
            }
        }

        private void ProcessQuery(EntityQuery query, bool markUpdated)
        {
            using var entities = query.ToEntityArray(Allocator.Temp);
            using var prefabRefs = query.ToComponentDataArray<PrefabRef>(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                Entity prefab = prefabRefs[i].m_Prefab;
                var lift = LiftFor(prefab);
                // Reading Lot waits for LotHeightSystem's job; only do it for buildings that are or were lifted.
                if (!lift.IsZero || _applied.ContainsKey(entities[i]))
                    Process(entities[i], lift, markUpdated);
                // A changed pad or cells only show once TerrainSystem redraws the lot.
                if (_changedPrefabs.Contains(prefab))
                    Commands().AddComponent<Updated>(entities[i]);
            }
        }

        private void Process(Entity entity, LotEdgeLift lift, bool markUpdated)
        {
            var lot = EntityManager.GetComponentData<Lot>(entity);
            bool had = _applied.TryGetValue(entity, out var applied);
            // A lot that differs from what was written has been recomputed by LotHeightSystem.
            var baseLot = had && SameHeights(lot, applied.Written) ? Shift(lot, applied.Lift, -1f) : lot;

            Lot target;
            if (lift.IsZero)
            {
                _applied.Remove(entity);
                target = baseLot;
            }
            else
            {
                target = Shift(baseLot, lift, 1f);
                _applied[entity] = new Applied(target, lift);
            }
            if (SameHeights(target, lot))
                return;

            EntityManager.SetComponentData(entity, target);
            // TerrainSystem only redraws lots of updated buildings.
            if (markUpdated)
                Commands().AddComponent<Updated>(entity);
        }

        private EntityCommandBuffer Commands()
        {
            if (!_hasCommands)
            {
                _commands = _barrier.CreateCommandBuffer();
                _hasCommands = true;
            }
            return _commands;
        }

        private LotEdgeLift LiftFor(Entity prefab)
        {
            if (!_slopes.TryGetValue(prefab, out var slope))
            {
                slope = PlanAndApply(prefab);
                _slopes[prefab] = slope;
            }
            return slope.EdgeLift;
        }

        private TerrainSlope PlanAndApply(Entity prefab)
        {
            if (!_variants.TryGetVariantCage(prefab, out var lotBox, out var offsets))
                return TerrainSlope.Empty;
            var slope = TerrainSlopePlanner.Plan(lotBox, offsets);
            if (slope.IsEmpty)
                return slope;
            try
            {
                if (!_applier.Apply(EntityManager, prefab, slope, lotBox.Center.xz))
                    return TerrainSlope.Empty;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Applying terrain slope to prefab {prefab} failed.");
                return TerrainSlope.Empty;
            }
            _changedPrefabs.Add(prefab);
            Mod.Log.Info($"Terrain slope for prefab {prefab}: {slope}");
            return slope;
        }

        private static Lot Shift(Lot lot, LotEdgeLift lift, float sign) => new()
        {
            m_FrontHeights = lot.m_FrontHeights + sign * lift.Front,
            m_RightHeights = lot.m_RightHeights + sign * lift.Right,
            m_BackHeights = lot.m_BackHeights + sign * lift.Back,
            m_LeftHeights = lot.m_LeftHeights + sign * lift.Left,
        };

        private static bool SameHeights(Lot a, Lot b) =>
            math.all(a.m_FrontHeights == b.m_FrontHeights) && math.all(a.m_RightHeights == b.m_RightHeights) &&
            math.all(a.m_BackHeights == b.m_BackHeights) && math.all(a.m_LeftHeights == b.m_LeftHeights);

        private readonly struct Applied
        {
            public Applied(Lot written, LotEdgeLift lift)
            {
                Written = written;
                Lift = lift;
            }

            public Lot Written { get; }
            public LotEdgeLift Lift { get; }
        }
    }
}
