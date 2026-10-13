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
    /// Path ends attached to a road's side slide along it; nodes shared with outside edges never move.
    /// </summary>
    internal static class NetInstanceSync
    {
        private const float GroundElevation = 0.01f;
        private const float EdgeEndCurvePosition = 1e-3f;

        private enum NodeKind
        {
            Internal,
            RoadSide,
            Fixed,
        }

        /// <param name="failed">Edges and nodes the old cage could not be inverted at; they keep their place.</param>
        /// <param name="slid">Path ends moved along the road they are attached to.</param>
        /// <returns>Number of edges rewritten.</returns>
        public static int Sync(EntityManager entityManager, Entity building, in Transform transform, LotRemap remap,
            out int failed, out int slid)
        {
            failed = 0;
            slid = 0;
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
            var roadEnds = new Dictionary<Entity, Entity[]>();
            var sideEdges = new List<Entity>();
            foreach (var node in nodes)
            {
                var kind = Classify(entityManager, node, owned, sideEdges);
                if (kind == NodeKind.Fixed)
                    continue;
                var local = ToBuilding(transform, entityManager.GetComponentData<NetNode>(node).m_Position);
                bool remapped;
                float3 result;
                if (kind == NodeKind.Internal)
                    remapped = remap.TryRemap(local, IsOnGround(entityManager, node), out result);
                else
                    remapped = remap.TryRemapAlong(local, RoadDirection(entityManager, transform, sideEdges[0], node),
                        out result);
                if (!remapped)
                {
                    failed++;
                    continue;
                }
                moved[node] = ToWorld(transform, result);
                if (kind == NodeKind.RoadSide)
                    roadEnds[node] = sideEdges.ToArray();
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

            foreach (var pair in roadEnds)
            {
                foreach (var road in pair.Value)
                    Reattach(entityManager, road, pair.Key, moved[pair.Key]);
                slid++;
            }
            return edges.Count;
        }

        // Road-side: every outside edge holds the node as a side connection, none as its start or end.
        private static NodeKind Classify(EntityManager entityManager, Entity node, HashSet<Entity> owned,
            List<Entity> sideEdges)
        {
            sideEdges.Clear();
            if (!entityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connected))
                return NodeKind.Internal;
            foreach (var connection in connected)
            {
                var edge = connection.m_Edge;
                if (owned.Contains(edge))
                    continue;
                var edgeData = entityManager.GetComponentData<Edge>(edge);
                if (edgeData.m_Start == node || edgeData.m_End == node ||
                    !TryFindSideConnection(entityManager, edge, node, out _))
                    return NodeKind.Fixed;
                sideEdges.Add(edge);
            }
            if (sideEdges.Count == 0)
                return NodeKind.Internal;
            return IsOnGround(entityManager, node) ? NodeKind.RoadSide : NodeKind.Fixed;
        }

        private static bool TryFindSideConnection(EntityManager entityManager, Entity edge, Entity node, out int index)
        {
            index = -1;
            if (!entityManager.TryGetBuffer(edge, true, out DynamicBuffer<ConnectedNode> sideNodes))
                return false;
            for (int i = 0; i < sideNodes.Length; i++)
            {
                if (sideNodes[i].m_Node == node)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        // Road tangent where the node is attached, in building space (xz).
        private static float2 RoadDirection(EntityManager entityManager, in Transform transform, Entity road,
            Entity node)
        {
            TryFindSideConnection(entityManager, road, node, out int index);
            float t = entityManager.GetBuffer<ConnectedNode>(road, true)[index].m_CurvePosition;
            var tangent = MathUtils.Tangent(entityManager.GetComponentData<Curve>(road).m_Bezier, t);
            return math.mul(math.inverse(transform.m_Rotation), tangent).xz;
        }

        // The game links side connections by curve position; point it at the node's new place on the road.
        private static void Reattach(EntityManager entityManager, Entity road, Entity node, float3 position)
        {
            if (!TryFindSideConnection(entityManager, road, node, out int index))
                return;
            var curve = entityManager.GetComponentData<Curve>(road).m_Bezier;
            MathUtils.Distance(curve.xz, position.xz, out float t);
            if (t <= EdgeEndCurvePosition || t >= 1f - EdgeEndCurvePosition)
                Mod.Log.Warn($"Path end {node} slid to the end of road {road}; it stays attached to that road edge.");
            var sideNodes = entityManager.GetBuffer<ConnectedNode>(road);
            sideNodes[index] = new ConnectedNode(node, t);
            AddUpdated(entityManager, road);
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
