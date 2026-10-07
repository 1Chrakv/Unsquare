using Game.Prefabs;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>Measures the undeformed cage box of a building from its render meshes (building space).</summary>
    internal static class CageBoxMeasurer
    {
        public static bool TryMeasure(ObjectGeometryPrefab prefab, out CageBox box)
        {
            box = default;
            if (prefab == null || prefab.m_Meshes == null)
                return false;

            var bounds = MinMaxBounds.Empty;
            foreach (var info in prefab.m_Meshes)
            {
                if (info == null || !(info.m_Mesh is RenderPrefab render))
                    continue;
                var transform = new SubmeshTransform(info.m_Position, info.m_Rotation);
                var local = render.bounds;
                // Rotated submeshes: encapsulate all 8 corners of their local bounds.
                for (int corner = 0; corner < CageTopology.CornerCount; corner++)
                {
                    float3 p = math.select(local.min, local.max, CageTopology.CornerSide(corner) == 1);
                    bounds = bounds.Encapsulate(transform.PointToBuilding(p));
                }
            }

            if (bounds.IsEmpty || math.any(bounds.Max - bounds.Min < CageBox.MinSize))
                return false;
            box = new CageBox(bounds.Min, bounds.Max);
            return true;
        }
    }
}
