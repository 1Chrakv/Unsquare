using Game.Common;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Points a building at another prefab, moves its saved lot entities along and makes dependent systems
    /// and the renderer refresh.
    /// </summary>
    internal static class PrefabSwapper
    {
        public static bool Swap(EntityManager entityManager, Entity building, Entity targetPrefab)
        {
            var current = entityManager.GetComponentData<PrefabRef>(building);
            if (current.m_Prefab == targetPrefab)
                return false;

            entityManager.SetComponentData(building, new PrefabRef(targetPrefab));
            LotInstanceSync.Sync(entityManager, building, current.m_Prefab, targetPrefab);
            // Updated: simulation systems re-evaluate; BatchesUpdated: renderer rebuilds mesh batches.
            if (!entityManager.HasComponent<Updated>(building))
                entityManager.AddComponent<Updated>(building);
            if (!entityManager.HasComponent<BatchesUpdated>(building))
                entityManager.AddComponent<BatchesUpdated>(building);
            return true;
        }
    }
}
