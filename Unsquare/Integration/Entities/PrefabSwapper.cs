using System;
using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Deformation;
using BuildingServiceUpgrade = Game.Buildings.ServiceUpgrade;
using InstalledUpgrade = Game.Buildings.InstalledUpgrade;

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
            RefreshUpgradeGroup(entityManager, building);
            return true;
        }

        // The swap rebuilds lanes; the rest of the group keeps LaneConnection references to the deleted ones
        // (a native crash later) unless the game re-checks its lanes too, which it only does for Updated entities.
        private static void RefreshUpgradeGroup(EntityManager entityManager, Entity swapped)
        {
            var root = swapped;
            if (entityManager.HasComponent<BuildingServiceUpgrade>(swapped) &&
                entityManager.TryGetComponent(swapped, out Owner owner) && IsAlive(entityManager, owner.m_Owner))
                root = owner.m_Owner;
            if (!entityManager.TryGetBuffer(root, true, out DynamicBuffer<InstalledUpgrade> installed) ||
                (installed.Length == 0 && root == swapped))
                return;

            var group = new List<Entity>(installed.Length + 1) { root };
            foreach (var upgrade in installed)
                group.Add(upgrade.m_Upgrade);
            int refreshed = 0;
            foreach (var entity in group)
            {
                if (!IsAlive(entityManager, entity) || entityManager.HasComponent<Updated>(entity))
                    continue;
                entityManager.AddComponent<Updated>(entity);
                refreshed++;
            }
            Mod.Log.Info($"Refreshed {refreshed} entities of the upgrade group of {root} after swapping {swapped} " +
                         $"({installed.Length} upgrades).");
        }

        private static bool IsAlive(EntityManager entityManager, Entity entity) =>
            entityManager.Exists(entity) && !entityManager.HasComponent<Deleted>(entity);

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
