using System;
using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Editing;
using NetSearchSystem = Game.Net.SearchSystem;
using ObjectSearchSystem = Game.Objects.SearchSystem;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Finds what lies around a building for <see cref="NeighbourAlign"/>: road edges (outer curves of road
    /// segments) and other buildings, via the game's search trees. Main thread only.
    /// </summary>
    internal sealed class NeighbourQuery
    {
        private const int PiecesPerCurve = 4;

        private readonly EntityManager _entityManager;
        private readonly NetSearchSystem _nets;
        private readonly ObjectSearchSystem _objects;
        private readonly List<Entity> _found = new();

        public NeighbourQuery(EntityManager entityManager, NetSearchSystem nets, ObjectSearchSystem objects)
        {
            _entityManager = entityManager;
            _nets = nets;
            _objects = objects;
        }

        /// <summary>Adds the road edges in the world xz area, as straight pieces in the building's space.</summary>
        public void CollectRoadEdges(in Transform building, Bounds2 area, List<NeighbourEdge> edges)
        {
            try
            {
                Search(_nets.GetNetSearchTree(true, out var dependencies), dependencies, area);
                foreach (var edge in _found)
                {
                    // Building sub-nets (paths) carry an Owner; only free-standing roads count.
                    if (!_entityManager.HasComponent<Road>(edge) || _entityManager.HasComponent<Owner>(edge) ||
                        _entityManager.HasComponent<Temp>(edge) || _entityManager.HasComponent<Deleted>(edge) ||
                        !_entityManager.HasComponent<EdgeGeometry>(edge))
                        continue;
                    var geometry = _entityManager.GetComponentData<EdgeGeometry>(edge);
                    AddCurve(building, geometry.m_Start.m_Left, edges);
                    AddCurve(building, geometry.m_Start.m_Right, edges);
                    AddCurve(building, geometry.m_End.m_Left, edges);
                    AddCurve(building, geometry.m_End.m_Right, edges);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Collecting road edges for neighbour alignment failed.");
            }
        }

        /// <summary>Adds the buildings in the world xz area except the target and buildings attached to it.</summary>
        public void CollectBuildings(Entity target, Bounds2 area, List<Entity> buildings)
        {
            try
            {
                Search(_objects.GetStaticSearchTree(true, out var dependencies), dependencies, area);
                foreach (var entity in _found)
                {
                    if (entity == target || !_entityManager.HasComponent<Building>(entity) ||
                        _entityManager.HasComponent<Temp>(entity) || _entityManager.HasComponent<Deleted>(entity) ||
                        IsOwnedBy(entity, target))
                        continue;
                    buildings.Add(entity);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Collecting buildings around {target} for neighbour alignment failed.");
            }
        }

        private void Search(NativeQuadTree<Entity, QuadTreeBoundsXZ> tree, Unity.Jobs.JobHandle dependencies,
            Bounds2 area)
        {
            dependencies.Complete();
            _found.Clear();
            var iterator = new Collector(area, _found);
            tree.Iterate(ref iterator);
        }

        private bool IsOwnedBy(Entity entity, Entity owner)
        {
            for (int depth = 0; depth < 8 && _entityManager.HasComponent<Owner>(entity); depth++)
            {
                entity = _entityManager.GetComponentData<Owner>(entity).m_Owner;
                if (entity == owner)
                    return true;
            }
            return false;
        }

        private static void AddCurve(in Transform building, Bezier4x3 curve, List<NeighbourEdge> edges)
        {
            float2 previous = ToBuilding(building, curve.a);
            for (int i = 1; i <= PiecesPerCurve; i++)
            {
                float2 next = ToBuilding(building, MathUtils.Position(curve, (float)i / PiecesPerCurve));
                edges.Add(new NeighbourEdge(previous, next, true));
                previous = next;
            }
        }

        private static float2 ToBuilding(in Transform building, float3 world) =>
            math.rotate(math.inverse(building.m_Rotation), world - building.m_Position).xz;

        private struct Collector : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            private readonly Bounds2 _area;
            private readonly List<Entity> _items;

            public Collector(Bounds2 area, List<Entity> items)
            {
                _area = area;
                _items = items;
            }

            public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds.xz, _area);

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (MathUtils.Intersect(bounds.m_Bounds.xz, _area))
                    _items.Add(item);
            }
        }
    }
}
