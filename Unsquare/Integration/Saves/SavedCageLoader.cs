using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Saves
{
    /// <summary>Reads the base and deformation a building was saved with.</summary>
    internal static class SavedCageLoader
    {
        /// <summary>
        /// True for a building on a plain geometry prefab with a readable, deformed <see cref="SavedCage"/>. Buildings
        /// already on a variant (restored or copied) are not saved shapes waiting to come back.
        /// </summary>
        public static bool TryRead(EntityManager entityManager, PrefabSystem prefabSystem, Entity building,
            List<int> scratch, out ObjectGeometryPrefab basePrefab, out Entity baseEntity, out DeformSpec spec)
        {
            basePrefab = null;
            baseEntity = Entity.Null;
            spec = null;
            if (!entityManager.TryGetComponent(building, out SavedCage cage) ||
                !entityManager.TryGetComponent(building, out PrefabRef prefabRef) ||
                !prefabSystem.TryGetPrefab(prefabRef, out PrefabBase prefab) ||
                !(prefab is ObjectGeometryPrefab geometry) || VariantNaming.IsVariantName(geometry.name) ||
                !SavedCages.TryRead(cage, scratch, out spec) || spec.IsIdentity)
                return false;
            basePrefab = geometry;
            baseEntity = prefabRef.m_Prefab;
            return true;
        }
    }
}
