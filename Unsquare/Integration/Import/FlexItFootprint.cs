using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;

namespace Unsquare.Integration.Import
{
    /// <summary>
    /// The prefab measures FlexIt's taper and shear are relative to, measured the way FlexIt does,
    /// so imported cages reproduce what the player saw.
    /// </summary>
    internal static class FlexItFootprint
    {
        private const float MinHalfExtent = 4f;

        /// <summary>Largest distance of a render mesh bound from the origin along x or z (submesh rotation ignored).</summary>
        public static float HalfExtent(ObjectGeometryPrefab prefab, bool alongX)
        {
            float extent = MinHalfExtent;
            if (prefab.m_Meshes == null)
                return extent;
            foreach (var info in prefab.m_Meshes)
            {
                if (info == null || !(info.m_Mesh is RenderPrefab render))
                    continue;
                float offset = alongX ? info.m_Position.x : info.m_Position.z;
                float min = offset + (alongX ? render.bounds.min.x : render.bounds.min.z);
                float max = offset + (alongX ? render.bounds.max.x : render.bounds.max.z);
                extent = math.max(extent, math.max(math.abs(min), math.abs(max)));
            }
            return extent;
        }

        /// <summary>Street-side edge of the prefab's geometry bounds, FlexIt's pivot depth.</summary>
        public static bool TryGetFrontZ(EntityManager entityManager, Entity prefabEntity, out float frontZ)
        {
            frontZ = 0f;
            if (!entityManager.TryGetComponent(prefabEntity, out ObjectGeometryData geometry))
                return false;
            frontZ = geometry.m_Bounds.max.z;
            return true;
        }
    }
}
