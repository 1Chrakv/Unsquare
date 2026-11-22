using System;
using Game;
using Game.Prefabs;
using Unity.Entities;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Runs right after the serializer in the save frame: puts the buildings <see cref="SavedCageWriteSystem"/> moved
    /// to their base back on their variants, before anything else sees them.
    /// </summary>
    public partial class SavedCageRestoreSystem : GameSystemBase
    {
        private VariantSystem _variants;

        protected override void OnCreate()
        {
            base.OnCreate();
            _variants = World.GetOrCreateSystemManaged<VariantSystem>();
        }

        protected override void OnUpdate()
        {
            try
            {
                int restored = _variants.SaveSwaps.RestoreAll(EntityManager);
                Mod.Log.Info($"Save: {restored} buildings back on their variants; " +
                             $"{CountSavedVariantPrefabs()} variant prefab records kept by the game.");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Putting deformed buildings back on their variants after the save failed.");
            }
        }

        // The game also saves prefabs for their unlock or budget state; such records of variants load as unused.
        private int CountSavedVariantPrefabs()
        {
            int count = 0;
            foreach (var prefab in _variants.VariantPrefabs)
            {
                if (EntityManager.Exists(prefab) && EntityManager.HasComponent<PrefabData>(prefab) &&
                    EntityManager.IsComponentEnabled<PrefabData>(prefab))
                    count++;
            }
            return count;
        }
    }
}
