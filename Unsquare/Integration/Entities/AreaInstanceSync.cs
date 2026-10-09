using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Matching;
using AreaNode = Game.Areas.Node;
using AreaSubArea = Game.Areas.SubArea;
using PrefabSubArea = Game.Prefabs.SubArea;
using SubAreaNode = Game.Prefabs.SubAreaNode;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Moves a building's existing lot areas (grass, paving, …) from the old prefab's layout to the new one.
    /// The game creates these once at placement and saves them, so a prefab swap alone never changes them.
    /// </summary>
    internal static class AreaInstanceSync
    {
        /// <param name="maxMatchDistance">Areas further off their expected outline (mean, meters) were edited.</param>
        /// <returns>Number of areas rewritten.</returns>
        public static int Sync(EntityManager entityManager, Entity building, in Transform transform,
            Entity oldPrefab, Entity newPrefab, float maxMatchDistance)
        {
            if (!entityManager.TryGetBuffer(building, true, out DynamicBuffer<AreaSubArea> subAreas) || subAreas.Length == 0)
                return 0;
            var expectedOld = ExpectedAreas(entityManager, oldPrefab, transform);
            var expectedNew = ExpectedAreas(entityManager, newPrefab, transform);
            if (expectedOld.Count == 0 || expectedOld.Count != expectedNew.Count)
                return 0;

            var areas = new List<Entity>(subAreas.Length);
            var current = new List<float3[]>(subAreas.Length);
            foreach (var subArea in subAreas)
            {
                if (!entityManager.TryGetBuffer(subArea.m_Area, true, out DynamicBuffer<AreaNode> nodes))
                    continue;
                areas.Add(subArea.m_Area);
                current.Add(Outline(nodes));
            }

            int[] match = GreedyMatcher.Match(areas.Count, expectedOld.Count,
                (i, j) => ShapeDistance.Polygon(current[i], Positions(expectedOld[j])), maxMatchDistance);

            int written = 0;
            for (int i = 0; i < areas.Count; i++)
            {
                if (match[i] < 0)
                    continue;
                var nodes = entityManager.GetBuffer<AreaNode>(areas[i]);
                var target = expectedNew[match[i]];
                // Keep the instance's format: with or without the repeated closing node.
                int count = nodes.Length == target.Length ? target.Length : target.Length - 1;
                nodes.Clear();
                for (int n = 0; n < count; n++)
                    nodes.Add(target[n]);
                AddUpdated(entityManager, areas[i]);
                written++;
            }
            return written;
        }

        // Mirrors how the object tool lays out sub-area nodes in world space (incl. its closing node).
        private static List<AreaNode[]> ExpectedAreas(EntityManager entityManager, Entity prefab, in Transform transform)
        {
            var result = new List<AreaNode[]>();
            if (!entityManager.TryGetBuffer(prefab, true, out DynamicBuffer<PrefabSubArea> subAreas) ||
                !entityManager.TryGetBuffer(prefab, true, out DynamicBuffer<SubAreaNode> subAreaNodes))
                return result;

            foreach (var subArea in subAreas)
            {
                int2 range = subArea.m_NodeRange;
                var nodes = new AreaNode[range.y - range.x + 1];
                int index = ObjectToolBaseSystem.GetFirstNodeIndex(subAreaNodes, range);
                for (int n = 0; n < nodes.Length; n++)
                {
                    var local = subAreaNodes[index];
                    float3 world = transform.m_Position + math.rotate(transform.m_Rotation, local.m_Position);
                    float elevation = local.m_ParentMesh >= 0 ? local.m_Position.y : float.MinValue;
                    nodes[n] = new AreaNode(world, elevation);
                    if (++index == range.y)
                        index = range.x;
                }
                result.Add(nodes);
            }
            return result;
        }

        private static float3[] Outline(DynamicBuffer<AreaNode> nodes)
        {
            var points = new float3[nodes.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = nodes[i].m_Position;
            return WithoutClosingNode(points);
        }

        private static float3[] Positions(AreaNode[] nodes)
        {
            var points = new float3[nodes.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = nodes[i].m_Position;
            return WithoutClosingNode(points);
        }

        private static float3[] WithoutClosingNode(float3[] points)
        {
            if (points.Length < 2 || math.distance(points[0].xz, points[points.Length - 1].xz) > 1e-3f)
                return points;
            var open = new float3[points.Length - 1];
            System.Array.Copy(points, open, open.Length);
            return open;
        }

        private static void AddUpdated(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Updated>(entity))
                entityManager.AddComponent<Updated>(entity);
        }
    }
}
