using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Objects;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Matching;
using Unsquare.Integration.Prefabs.Attachments;
using NetNode = Game.Net.Node;
using NetSubNet = Game.Net.SubNet;
using PrefabSubNet = Game.Prefabs.SubNet;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Moves a building's existing lot nets (paths, …) from the old prefab's layout to the new one. Nodes shared
    /// with nets outside the building (e.g. a road) stay put, so external connections never move.
    /// </summary>
    internal static class NetInstanceSync
    {
        /// <param name="maxMatchDistance">Edges further off their expected curve (mean, meters) were edited.</param>
        /// <returns>Number of edges rewritten.</returns>
        public static int Sync(EntityManager entityManager, Entity building, in Transform transform,
            Entity oldPrefab, Entity newPrefab, float maxMatchDistance)
        {
            if (!entityManager.TryGetBuffer(building, true, out DynamicBuffer<NetSubNet> subNets) || subNets.Length == 0)
                return 0;
            var expectedOld = ExpectedCurves(entityManager, oldPrefab, transform, out _);
            var expectedNew = ExpectedCurves(entityManager, newPrefab, transform, out var onGround);
            if (expectedOld.Count == 0 || expectedOld.Count != expectedNew.Count)
                return 0;

            var owned = new HashSet<Entity>();
            var edges = new List<Entity>();
            foreach (var subNet in subNets)
            {
                owned.Add(subNet.m_SubNet);
                if (entityManager.HasComponent<Edge>(subNet.m_SubNet) && entityManager.HasComponent<Curve>(subNet.m_SubNet))
                    edges.Add(subNet.m_SubNet);
            }

            var currentCurves = new List<Bezier4x3>(edges.Count);
            foreach (var edge in edges)
                currentCurves.Add(entityManager.GetComponentData<Curve>(edge).m_Bezier);
            var reversed = new bool[edges.Count, expectedOld.Count];
            int[] match = GreedyMatcher.Match(edges.Count, expectedOld.Count, (i, j) =>
            {
                float distance = ShapeDistance.Curve(CurveConversions.ToCore(currentCurves[i]),
                    CurveConversions.ToCore(expectedOld[j]), out bool flip);
                reversed[i, j] = flip;
                return distance;
            }, maxMatchDistance);

            // Target curves per matched edge, then node targets from their endpoints.
            var targets = new Dictionary<Entity, Bezier4x3>();
            var nodeTargets = new Dictionary<Entity, float4>();
            for (int i = 0; i < edges.Count; i++)
            {
                int j = match[i];
                if (j < 0)
                    continue;
                var target = expectedNew[j];
                if (reversed[i, j])
                    target = Reverse(target);
                if (onGround[j])
                    target = WithHeights(target, currentCurves[i]);
                targets[edges[i]] = target;
                var edgeData = entityManager.GetComponentData<Edge>(edges[i]);
                Accumulate(nodeTargets, edgeData.m_Start, target.a);
                Accumulate(nodeTargets, edgeData.m_End, target.d);
            }

            var moved = new Dictionary<Entity, float3>();
            foreach (var pair in nodeTargets)
            {
                if (IsInternal(entityManager, pair.Key, owned))
                    moved[pair.Key] = pair.Value.xyz / pair.Value.w;
            }

            foreach (var pair in targets)
            {
                var edgeData = entityManager.GetComponentData<Edge>(pair.Key);
                var curve = pair.Value;
                curve.a = NodePosition(entityManager, edgeData.m_Start, moved);
                curve.d = NodePosition(entityManager, edgeData.m_End, moved);
                entityManager.SetComponentData(pair.Key, new Curve { m_Bezier = curve, m_Length = MathUtils.Length(curve) });
                AddUpdated(entityManager, pair.Key);
            }
            foreach (var pair in moved)
            {
                var node = entityManager.GetComponentData<NetNode>(pair.Key);
                node.m_Position = pair.Value;
                entityManager.SetComponentData(pair.Key, node);
                AddUpdated(entityManager, pair.Key);
            }
            return targets.Count;
        }

        private static List<Bezier4x3> ExpectedCurves(EntityManager entityManager, Entity prefab, in Transform transform,
            out List<bool> onGround)
        {
            var result = new List<Bezier4x3>();
            onGround = new List<bool>();
            if (!entityManager.TryGetBuffer(prefab, true, out DynamicBuffer<PrefabSubNet> subNets))
                return result;
            foreach (var subNet in subNets)
            {
                result.Add(ObjectUtils.LocalToWorld(transform.m_Position, transform.m_Rotation, subNet.m_Curve));
                onGround.Add(subNet.m_ParentMesh.x < 0 && subNet.m_ParentMesh.y < 0);
            }
            return result;
        }

        // A node is ours to move only if every edge it connects belongs to the building.
        private static bool IsInternal(EntityManager entityManager, Entity node, HashSet<Entity> owned)
        {
            if (!owned.Contains(node) || !entityManager.HasComponent<NetNode>(node))
                return false;
            if (!entityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connected))
                return true;
            foreach (var edge in connected)
            {
                if (!owned.Contains(edge.m_Edge))
                    return false;
            }
            return true;
        }

        private static float3 NodePosition(EntityManager entityManager, Entity node, Dictionary<Entity, float3> moved) =>
            moved.TryGetValue(node, out var position) ? position : entityManager.GetComponentData<NetNode>(node).m_Position;

        private static void Accumulate(Dictionary<Entity, float4> sums, Entity node, float3 position)
        {
            sums.TryGetValue(node, out var sum);
            sums[node] = sum + new float4(position, 1f);
        }

        private static Bezier4x3 Reverse(Bezier4x3 curve) => new(curve.d, curve.c, curve.b, curve.a);

        // Ground nets follow the (flattened) lot terrain: keep the existing heights.
        private static Bezier4x3 WithHeights(Bezier4x3 target, Bezier4x3 current)
        {
            target.a.y = current.a.y;
            target.b.y = current.b.y;
            target.c.y = current.c.y;
            target.d.y = current.d.y;
            return target;
        }

        private static void AddUpdated(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Updated>(entity))
                entityManager.AddComponent<Updated>(entity);
        }
    }
}
