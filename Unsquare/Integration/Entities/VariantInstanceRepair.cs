using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// Buildings created from a variant prefab before its archetype was completed (copies in older saves) lack the
    /// added types. Gives each such building the types its variant's archetype gained.
    /// </summary>
    internal static class VariantInstanceRepair
    {
        /// <param name="gaps">Per variant prefab, the types its archetype gained.</param>
        /// <returns>Number of buildings that got at least one type.</returns>
        public static int AddMissing(EntityManager entityManager, NativeArray<Entity> buildings,
            Dictionary<Entity, ComponentType[]> gaps)
        {
            int repaired = 0;
            foreach (var building in buildings)
            {
                if (!entityManager.TryGetComponent(building, out PrefabRef prefab) ||
                    !gaps.TryGetValue(prefab.m_Prefab, out var types))
                    continue;
                bool changed = false;
                foreach (var type in types)
                {
                    if (entityManager.HasComponent(building, type))
                        continue;
                    entityManager.AddComponent(building, type);
                    changed = true;
                }
                if (changed)
                    repaired++;
            }
            return repaired;
        }
    }
}
