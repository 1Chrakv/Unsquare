using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Deformation;
using Unsquare.Integration.Prefabs.Attachments;
using NetNode = Game.Net.Node;
using NetSubNet = Game.Net.SubNet;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Moves a building's existing lot nets (paths, …) from the old cage deformation to the new one, point by point.
    /// The game splits prefab curves at junctions, so edges are remapped rather than matched to prefab curves.
    /// Nodes shared with nets outside the building (e.g. a road) stay put, so external connections never move.
    /// </summary>
    internal static class NetInstanceSync
    {
        private const float GroundElevation = 0.01f;

        /// <param name="failed">Edges and nodes the old cage could not be inverted at; they keep their place.</param>
        /// <returns>Number of edges rewritten.</returns>
        public static int Sync(EntityManager entityManager, Entity building, in Transform transform, LotRemap remap,
            out int failed)
        {
            failed = 0;
            if (!entityManager.TryGetBuffer(building, true, out DynamicBuffer<NetSubNet> subNets) || subNets.Length == 0)
                return 0;

            var owned = new HashSet<Entity>();
            var edges = new List<Entity>();
            var nodes = new List<Entity>();
            foreach (var subNet in subNets)
            {
                var net = subNet.m_SubNet;
                owned.Add(net);
                if (entityManager.HasComponent<Edge>(net) && entityManager.HasComponent<Curve>(net))
                    edges.Add(net);
                else if (entityManager.HasComponent<NetNode>(net))
                    nodes.Add(net);
            }

            var moved = new Dictionary<Entity, float3>();
            foreach (var node in nodes)
            {
                if (!IsInternal(entityManager, node, owned))
                    continue;
                var position = entityManager.GetComponentData<NetNode>(node).m_Position;
                if (remap.TryRemap(ToBuilding(transform, position), IsOnGround(entityManager, node), out var local))
                    moved[node] = ToWorld(transform, local);
                else
                    failed++;
            }

            foreach (var edge in edges)
            {
                var curve = ToBuilding(transform, entityManager.GetComponentData<Curve>(edge).m_Bezier);
                if (remap.TryRemap(CurveConversions.ToCore(curve), IsOnGround(entityManager, edge), out var remapped))
                    curve = CurveConversions.ToGame(remapped);
                else
                    failed++;
                // Ends always sit on their nodes, moved or not, so the network stays connected.
                var target = ToWorld(transform, curve);
                var edgeData = entityManager.GetComponentData<Edge>(edge);
                target.a = NodePosition(entityManager, edgeData.m_Start, moved);
                target.d = NodePosition(entityManager, edgeData.m_End, moved);
                entityManager.SetComponentData(edge, new Curve { m_Bezier = target, m_Length = MathUtils.Length(target) });
                AddUpdated(entityManager, edge);
            }

            foreach (var pair in moved)
            {
                var node = entityManager.GetComponentData<NetNode>(pair.Key);
                node.m_Position = pair.Value;
                entityManager.SetComponentData(pair.Key, node);
                AddUpdated(entityManager, pair.Key);
            }
            return edges.Count;
        }

        // A node is ours to move only if every edge it connects belongs to the building.
        private static bool IsInternal(EntityManager entityManager, Entity node, HashSet<Entity> owned)
        {
            if (!entityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connected))
                return true;
            foreach (var edge in connected)
            {
                if (!owned.Contains(edge.m_Edge))
                    return false;
            }
            return true;
        }

        // Ground nets follow the terrain, so only their ground position is remapped (as the prefab bakes them).
        private static bool IsOnGround(EntityManager entityManager, Entity net) =>
            !entityManager.TryGetComponent(net, out Elevation elevation) ||
            math.all(math.abs(elevation.m_Elevation) < GroundElevation);

        private static float3 NodePosition(EntityManager entityManager, Entity node, Dictionary<Entity, float3> moved) =>
            moved.TryGetValue(node, out var position) ? position : entityManager.GetComponentData<NetNode>(node).m_Position;

        private static float3 ToBuilding(in Transform transform, float3 world) =>
            math.mul(math.inverse(transform.m_Rotation), world - transform.m_Position);

        private static float3 ToWorld(in Transform transform, float3 local) =>
            transform.m_Position + math.mul(transform.m_Rotation, local);

        private static Bezier4x3 ToBuilding(in Transform transform, Bezier4x3 curve) => new(
            ToBuilding(transform, curve.a), ToBuilding(transform, curve.b),
            ToBuilding(transform, curve.c), ToBuilding(transform, curve.d));

        private static Bezier4x3 ToWorld(in Transform transform, Bezier4x3 curve) => new(
            ToWorld(transform, curve.a), ToWorld(transform, curve.b),
            ToWorld(transform, curve.c), ToWorld(transform, curve.d));

        private static void AddUpdated(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Updated>(entity))
                entityManager.AddComponent<Updated>(entity);
        }
    }
}
