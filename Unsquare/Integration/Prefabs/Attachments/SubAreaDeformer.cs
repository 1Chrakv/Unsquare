using Game.Prefabs;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>
    /// Deforms lot areas (grass, paving, …) in the variant's prefab data. Existing area entities are updated
    /// separately after the swap, since the game creates them once at placement.
    /// </summary>
    internal sealed class SubAreaDeformer : IAttachmentDeformer
    {
        public AttachmentCount Apply(ObjectGeometryPrefab clone, ObjectGeometryPrefab source, AttachmentContext context)
        {
            var subAreas = PrefabCloning.GetOwnedComponent<ObjectSubAreas>(clone, source);
            if (subAreas == null || subAreas.m_SubAreas == null)
                return new AttachmentCount("areas", 0, 0);

            int deformedCount = 0;
            var result = new ObjectSubAreaInfo[subAreas.m_SubAreas.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var original = subAreas.m_SubAreas[i];
                result[i] = original;
                if (original?.m_NodePositions == null || !context.Affects(Centroid(original.m_NodePositions)))
                    continue;
                if (TryDeform(original, context, out var deformed))
                {
                    result[i] = deformed;
                    deformedCount++;
                }
                else
                {
                    Mod.Log.Warn($"Area {i} of {source.name} would self-intersect; kept undeformed.");
                }
            }
            subAreas.m_SubAreas = result;
            return new AttachmentCount("areas", result.Length, deformedCount);
        }

        private static bool TryDeform(ObjectSubAreaInfo original, AttachmentContext context, out ObjectSubAreaInfo deformed)
        {
            var nodes = original.m_NodePositions;
            var positions = new float3[nodes.Length];
            var outline = new float2[nodes.Length];
            for (int n = 0; n < nodes.Length; n++)
            {
                float3 p = context.Deformation.Apply(nodes[n], out _);
                if (IsGround(original, n))
                    p.y = nodes[n].y; // projected onto the terrain anyway
                positions[n] = p;
                outline[n] = p.xz;
            }
            deformed = null;
            if (!PolygonCheck.IsSimple(outline))
                return false;
            deformed = PrefabCloning.ShallowCopy(original);
            deformed.m_NodePositions = positions;
            return true;
        }

        private static bool IsGround(ObjectSubAreaInfo info, int node) =>
            info.m_ParentMeshes == null || node >= info.m_ParentMeshes.Length || info.m_ParentMeshes[node] < 0;

        private static float3 Centroid(float3[] nodes)
        {
            float3 sum = float3.zero;
            foreach (var node in nodes)
                sum += node;
            return nodes.Length == 0 ? sum : sum / nodes.Length;
        }
    }
}
