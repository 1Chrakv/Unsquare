using System;
using System.Collections.Generic;
using System.Reflection;
using Colossal.Entities;
using Game.Prefabs;
using Unity.Entities;
using Hash128 = Colossal.Hash128;
using Unsquare.Core.Cage;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Finds prefabs by type + name and maps variant prefabs back to their base and offsets.
    /// Deformations are always applied to the base, so they never stack.
    /// </summary>
    internal sealed class PrefabResolver
    {
        private static readonly FieldInfo PrefabListField =
            typeof(PrefabSystem).GetField("m_Prefabs", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly PrefabSystem _prefabSystem;
        private readonly Dictionary<string, VariantInfo> _variants = new(StringComparer.Ordinal);

        public PrefabResolver(PrefabSystem prefabSystem)
        {
            _prefabSystem = prefabSystem;
        }

        /// <summary>Lookup by PrefabID, falling back to a scan for prefabs not indexed yet (e.g. during preload).</summary>
        public bool TryFind(string typeName, string name, out PrefabBase prefab)
        {
            if (_prefabSystem.TryGetPrefab(new PrefabID(typeName, name, default(Hash128)), out prefab) && prefab != null)
                return true;
            prefab = ScanPrefabList(typeName, name);
            return prefab != null;
        }

        public bool TryGetPrefab(EntityManager entityManager, Entity building, out PrefabBase prefab)
        {
            prefab = null;
            return entityManager.Exists(building) &&
                   entityManager.TryGetComponent(building, out PrefabRef prefabRef) &&
                   _prefabSystem.TryGetPrefab(prefabRef, out prefab);
        }

        public void RememberVariant(string variantName, VariantInfo info) => _variants[variantName] = info;

        public bool TryGetVariant(string variantName, out VariantInfo info) => _variants.TryGetValue(variantName, out info);

        /// <summary>Base prefab and applied offsets of any building prefab (identity for non-variants).</summary>
        public bool TryResolveBase(PrefabBase current, out ObjectGeometryPrefab basePrefab, out CageOffsets applied)
        {
            applied = CageOffsets.Identity;
            basePrefab = null;
            if (current == null)
                return false;
            if (!VariantNaming.TryParse(current.name, out string baseName))
            {
                basePrefab = current as ObjectGeometryPrefab;
                return basePrefab != null;
            }
            if (_variants.TryGetValue(current.name, out var info))
            {
                basePrefab = info.BasePrefab;
                applied = info.Offsets;
                return true;
            }
            // Unknown variant (e.g. manifest lost): fall back to the base without offsets.
            basePrefab = TryFind(current.GetType().Name, baseName, out var found) ? found as ObjectGeometryPrefab : null;
            return basePrefab != null;
        }

        public void Clear() => _variants.Clear();

        private PrefabBase ScanPrefabList(string typeName, string name)
        {
            if (!(PrefabListField?.GetValue(_prefabSystem) is List<PrefabBase> prefabs))
                return null;
            foreach (var prefab in prefabs)
            {
                if (prefab != null && prefab.name == name && prefab.GetType().Name == typeName)
                    return prefab;
            }
            return null;
        }
    }
}
