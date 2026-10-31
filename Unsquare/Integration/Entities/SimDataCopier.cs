using Colossal.Entities;
using Game.Objects;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Variants lose SpawnableBuilding (to stay out of the spawner) and with it the zone's prefab data. Copies it
    /// from the base prefab so residents, jobs and level stay, and restores the spawnable zone flags.
    /// </summary>
    internal static class SimDataCopier
    {
        // Initialized as a ploppable, a variant overrides its lot cells to "no zone" and wipes the zoning.
        private const GeometryFlags ZoneFlags =
            GeometryFlags.OverrideZone | GeometryFlags.Overridable | GeometryFlags.DeleteOverridden;

        public static void CopyMissing(EntityManager entityManager, Entity basePrefab, Entity variantPrefab)
        {
            CopyZoneFlags(entityManager, basePrefab, variantPrefab);
            // Never BuildingSpawnGroupData: the zone spawner would start placing the variant.
            CopyIfMissing<SpawnableBuildingData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<BuildingPropertyData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<ConsumptionData>(entityManager, basePrefab, variantPrefab);
            CopyIfMissing<PollutionData>(entityManager, basePrefab, variantPrefab);
            CopyTagIfMissing<OfficeBuilding>(entityManager, basePrefab, variantPrefab);
            CopyBufferIfMissing<LevelUpResourceData>(entityManager, basePrefab, variantPrefab);
        }

        private static void CopyZoneFlags(EntityManager entityManager, Entity from, Entity to)
        {
            if (!entityManager.TryGetComponent(from, out ObjectGeometryData source) ||
                !entityManager.TryGetComponent(to, out ObjectGeometryData target))
                return;
            var flags = (target.m_Flags & ~ZoneFlags) | (source.m_Flags & ZoneFlags);
            if (flags == target.m_Flags)
                return;
            target.m_Flags = flags;
            entityManager.SetComponentData(to, target);
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
