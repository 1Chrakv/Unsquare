using Game.Prefabs;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Measures the undeformed cage box of a building in building space: its render meshes (Building scope)
    /// or its whole lot rectangle (Lot scope, buildings only).
    /// </summary>
    internal static class CageBoxMeasurer
    {
        public static bool SupportsLot(ObjectGeometryPrefab prefab) =>
            prefab is BuildingPrefab building && building.m_LotWidth > 0 && building.m_LotDepth > 0;

        public static bool TryMeasure(ObjectGeometryPrefab prefab, CageScope scope, out CageBox box)
        {
            box = default;
            var bounds = MeasureMeshes(prefab);
            if (scope == CageScope.Lot)
            {
                if (!SupportsLot(prefab))
                    return false;
                var building = (BuildingPrefab)prefab;
                box = LotBox.FromLot(new int2(building.m_LotWidth, building.m_LotDepth), bounds);
                return true;
            }

            if (bounds.IsEmpty || math.any(bounds.Max - bounds.Min < CageBox.MinSize))
                return false;
            box = new CageBox(bounds.Min, bounds.Max);
            return true;
        }

        private static MinMaxBounds MeasureMeshes(ObjectGeometryPrefab prefab)
        {
            var bounds = MinMaxBounds.Empty;
            if (prefab == null || prefab.m_Meshes == null)
                return bounds;
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
            return bounds;
        }
    }
}
