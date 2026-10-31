using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Upgrades register only with the building prefabs they list, never with a variant. Without that list a deformed
    /// building hides its Upgrades section and offers no upgrades, and placing one doesn't accept the variant as owner.
    /// </summary>
    internal static class UpgradeOwnership
    {
        /// <summary>Copies the base's upgrade list to the variant and lists the variant with those upgrades.</summary>
        /// <returns>Number of upgrades the variant now shares with its base.</returns>
        public static int Share(EntityManager entityManager, PrefabSystem prefabSystem, Entity basePrefab,
            Entity variantPrefab)
        {
            if (!entityManager.TryGetBuffer(basePrefab, true, out DynamicBuffer<BuildingUpgradeElement> baseUpgrades))
                return 0;
            var upgrades = new List<Entity>(baseUpgrades.Length);
            foreach (var element in baseUpgrades)
            {
                if (!IsVariant(prefabSystem, element.m_Upgrade))
                    upgrades.Add(element.m_Upgrade);
            }

            // AddBuffer is a structural change: the base buffer was read before it.
            var variantUpgrades = entityManager.HasBuffer<BuildingUpgradeElement>(variantPrefab)
                ? entityManager.GetBuffer<BuildingUpgradeElement>(variantPrefab)
                : entityManager.AddBuffer<BuildingUpgradeElement>(variantPrefab);
            foreach (var upgrade in upgrades)
            {
                if (!Contains(variantUpgrades, upgrade))
                    variantUpgrades.Add(new BuildingUpgradeElement(upgrade));
            }

            foreach (var upgrade in upgrades)
            {
                if (!entityManager.TryGetBuffer(upgrade, false, out DynamicBuffer<ServiceUpgradeBuilding> owners))
                    continue;
                bool listed = false;
                foreach (var owner in owners)
                    listed |= owner.m_Building == variantPrefab;
                // Appended: BuildingInitializeSystem derives an extension's lot from the first entry only.
                if (!listed)
                    owners.Add(new ServiceUpgradeBuilding(variantPrefab));
            }
            return upgrades.Count;
        }

        private static bool Contains(DynamicBuffer<BuildingUpgradeElement> upgrades, Entity upgrade)
        {
            foreach (var element in upgrades)
            {
                if (element.m_Upgrade == upgrade)
                    return true;
            }
            return false;
        }

        private static bool IsVariant(PrefabSystem prefabSystem, Entity prefab) =>
            prefabSystem.TryGetPrefab(prefab, out PrefabBase prefabBase) && prefabBase != null &&
            VariantNaming.IsVariantName(prefabBase.name);
    }
}
