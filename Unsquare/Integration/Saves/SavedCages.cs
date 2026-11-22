using System.Collections.Generic;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;

namespace Unsquare.Integration.Saves
{
    /// <summary>
    /// Converts between <see cref="SavedCage"/> and <see cref="DeformSpec"/> through the Core codec, and reads the base
    /// and deformation a building was saved with.
    /// </summary>
    internal static class SavedCages
    {
        /// <summary>False if a value does not fit the component (offsets beyond ±3 km).</summary>
        public static bool TryFrom(DeformSpec spec, List<int> scratch, out SavedCage cage)
        {
            SavedCageCodec.Encode(spec, scratch);
            cage = new SavedCage { Format = SavedCageCodec.FormatVersion };
            if (scratch.Count > cage.Values.Capacity)
                return false;
            foreach (int value in scratch)
            {
                if (value < short.MinValue || value > short.MaxValue)
                    return false;
                cage.Values.Add((short)value);
            }
            return true;
        }

        public static bool TryRead(in SavedCage cage, List<int> scratch, out DeformSpec spec)
        {
            scratch.Clear();
            for (int i = 0; i < cage.Values.Length; i++)
                scratch.Add(cage.Values[i]);
            return SavedCageCodec.TryDecode(cage.Format, scratch, out spec);
        }

        /// <summary>
        /// True for a building on a plain geometry prefab with a readable, deformed <see cref="SavedCage"/>. Buildings
        /// already on a variant (restored or copied) are not saved shapes waiting to come back.
        /// </summary>
        public static bool TryReadBuilding(EntityManager entityManager, PrefabSystem prefabSystem, Entity building,
            List<int> scratch, out ObjectGeometryPrefab basePrefab, out Entity baseEntity, out DeformSpec spec)
        {
            basePrefab = null;
            baseEntity = Entity.Null;
            spec = null;
            if (!entityManager.TryGetComponent(building, out SavedCage cage) ||
                !entityManager.TryGetComponent(building, out PrefabRef prefabRef) ||
                !prefabSystem.TryGetPrefab(prefabRef, out PrefabBase prefab) ||
                !(prefab is ObjectGeometryPrefab geometry) || VariantNaming.IsVariantName(geometry.name) ||
                !TryRead(cage, scratch, out spec) || spec.IsIdentity)
                return false;
            basePrefab = geometry;
            baseEntity = prefabRef.m_Prefab;
            return true;
        }
    }
}
