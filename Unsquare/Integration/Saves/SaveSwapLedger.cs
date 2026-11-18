using System.Collections.Generic;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Saves
{
    /// <summary>
    /// Buildings and extensions moved to their base prefab for a save, with the variant each was on; written back
    /// right after the serializer (or on the next update if the save failed in between).
    /// </summary>
    internal sealed class SaveSwapLedger
    {
        private readonly List<(Entity Entity, Entity Variant)> _swapped = new();
        private readonly HashSet<Entity> _entities = new();

        public int Count => _swapped.Count;

        public bool Contains(Entity entity) => _entities.Contains(entity);

        /// <summary>Points the entity at <paramref name="basePrefab"/> and remembers its variant.</summary>
        public void Swap(EntityManager entityManager, Entity entity, Entity variant, Entity basePrefab)
        {
            if (!_entities.Add(entity))
                return;
            _swapped.Add((entity, variant));
            entityManager.SetComponentData(entity, new PrefabRef(basePrefab));
        }

        /// <summary>Puts every recorded entity back on its variant; returns how many.</summary>
        public int RestoreAll(EntityManager entityManager)
        {
            int restored = 0;
            foreach (var (entity, variant) in _swapped)
            {
                if (!entityManager.Exists(entity) || entityManager.HasComponent<Deleted>(entity) ||
                    !entityManager.HasComponent<PrefabRef>(entity) || !entityManager.Exists(variant))
                    continue;
                entityManager.SetComponentData(entity, new PrefabRef(variant));
                restored++;
            }
            Clear();
            return restored;
        }

        public void Clear()
        {
            _swapped.Clear();
            _entities.Clear();
        }
    }
}
