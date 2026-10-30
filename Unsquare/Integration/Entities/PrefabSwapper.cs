using System;
using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Points a building and its upgrade extensions at other prefabs, moves their saved lot entities along and makes
    /// dependent systems and the renderer refresh.
    /// </summary>
    internal static class PrefabSwapper
    {
        public static bool Swap(EntityManager entityManager, Entity building, Entity targetPrefab,
            float areaMatchMeters, LotRemap netRemap) =>
            Swap(entityManager, building, targetPrefab, Array.Empty<ExtensionSwap>(), areaMatchMeters, netRemap);

        public static bool Swap(EntityManager entityManager, Entity building, Entity targetPrefab,
            IReadOnlyList<ExtensionSwap> extensions, float areaMatchMeters, LotRemap netRemap)
        {
            var current = entityManager.GetComponentData<PrefabRef>(building);
            bool buildingChanged = current.m_Prefab != targetPrefab;
            var changed = new List<(ExtensionSwap Swap, Entity OldPrefab)>(extensions.Count);
            foreach (var swap in extensions)
            {
                if (!entityManager.Exists(swap.Extension) || entityManager.HasComponent<Deleted>(swap.Extension))
                    continue;
                var old = entityManager.GetComponentData<PrefabRef>(swap.Extension);
                if (old.m_Prefab != swap.TargetPrefab)
                    changed.Add((swap, old.m_Prefab));
            }
            if (!buildingChanged && changed.Count == 0)
                return false;

            if (buildingChanged)
                entityManager.SetComponentData(building, new PrefabRef(targetPrefab));
            foreach (var (swap, _) in changed)
                entityManager.SetComponentData(swap.Extension, new PrefabRef(swap.TargetPrefab));
            LotInstanceSync.Sync(entityManager, building, current.m_Prefab, targetPrefab, changed, areaMatchMeters,
                buildingChanged ? netRemap : null);

            if (buildingChanged)
                MarkUpdated(entityManager, building);
            foreach (var (swap, _) in changed)
                MarkUpdated(entityManager, swap.Extension);
            return true;
        }

        // Updated: simulation systems re-evaluate; BatchesUpdated: renderer rebuilds mesh batches.
        private static void MarkUpdated(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.HasComponent<Updated>(entity))
                entityManager.AddComponent<Updated>(entity);
            if (!entityManager.HasComponent<BatchesUpdated>(entity))
                entityManager.AddComponent<BatchesUpdated>(entity);
        }
    }
}
