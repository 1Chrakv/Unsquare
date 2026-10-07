using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Variants lose SpawnableBuilding (to stay out of the spawner), so their prefab entity misses the
    /// zoning data a growable needs. Copies it from the base prefab entity so residents, jobs and level stay.
    /// </summary>
    internal static class SimDataCopier
    {
        public static void CopyMissing(EntityManager entityManager, Entity basePrefab, Entity variantPrefab)
        {
            CopyIfMissing<SpawnableBuildingData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<BuildingPropertyData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<ConsumptionData>(entityManager, basePrefab, variantPrefab);
        }

        private static void CopyIfMissing<T>(EntityManager entityManager, Entity from, Entity to)
            where T : unmanaged, IComponentData
        {
            if (entityManager.TryGetComponent(from, out T data) && !entityManager.HasComponent<T>(to))
                entityManager.AddComponentData(to, data);
        }
    }
}
