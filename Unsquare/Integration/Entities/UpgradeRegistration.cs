using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// A cloned upgrade prefab registers itself with the buildings it upgrades (ServiceUpgrade.LateInitialize),
    /// which would offer the variant in their upgrade menus. Undoes that, as the game does for removed prefabs.
    /// </summary>
    internal static class UpgradeRegistration
    {
        /// <returns>Number of buildings that listed the variant.</returns>
        public static int RemoveFromBuildings(EntityManager entityManager, Entity upgradeVariant)
        {
            if (!entityManager.TryGetBuffer(upgradeVariant, true, out DynamicBuffer<ServiceUpgradeBuilding> buildings))
                return 0;
            var targets = buildings.ToNativeArray(Unity.Collections.Allocator.Temp);
            int removed = 0;
            foreach (var building in targets)
            {
                if (!entityManager.TryGetBuffer(building.m_Building, false, out DynamicBuffer<BuildingUpgradeElement> upgrades))
                    continue;
                for (int i = upgrades.Length - 1; i >= 0; i--)
                {
                    if (upgrades[i].m_Upgrade != upgradeVariant)
                        continue;
                    upgrades.RemoveAt(i);
                    removed++;
                }
            }
            targets.Dispose();
            return removed;
        }
    }
}
