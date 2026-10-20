using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Variants lose SpawnableBuilding (to stay out of the spawner) and with it the zone's prefab data. Copies it
    /// from the base prefab entity so residents, jobs, level, address and the info panel sections stay.
    /// </summary>
    internal static class SimDataCopier
    {
        public static void CopyMissing(EntityManager entityManager, Entity basePrefab, Entity variantPrefab)
        {
            // Never BuildingSpawnGroupData: the zone spawner would start placing the variant.
            CopyIfMissing<SpawnableBuildingData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<BuildingPropertyData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<ConsumptionData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<PollutionData>(entityManager, basePrefab, variantPrefab);
            CopyTagIfMissing<OfficeBuilding>(entityManager, basePrefab, variantPrefab);
            CopyBufferIfMissing<LevelUpResourceData>(entityManager, basePrefab, variantPrefab);
        }

        private static void CopyIfMissing<T>(EntityManager entityManager, Entity from, Entity to)
            where T : unmanaged, IComponentData
        {
            if (entityManager.TryGetComponent(from, out T data) && !entityManager.HasComponent<T>(to))
                entityManager.AddComponentData(to, data);
        }

        private static void CopyTagIfMissing<T>(EntityManager entityManager, Entity from, Entity to)
            where T : unmanaged, IComponentData
        {
            if (entityManager.HasComponent<T>(from) && !entityManager.HasComponent<T>(to))
                entityManager.AddComponent<T>(to);
        }

        private static void CopyBufferIfMissing<T>(EntityManager entityManager, Entity from, Entity to)
            where T : unmanaged, IBufferElementData
        {
            if (!entityManager.HasBuffer<T>(from) || entityManager.HasBuffer<T>(to))
                return;
            // AddBuffer is a structural change: fetch the source buffer only afterwards.
            var target = entityManager.AddBuffer<T>(to);
            target.CopyFrom(entityManager.GetBuffer<T>(from, true));
        }
    }
}
