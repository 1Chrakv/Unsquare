using System;
using Colossal.Entities;
using Unity.Entities;
using Unsquare.Core.Deformation;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// After a prefab swap, moves the building's saved lot entities (areas, nets) to the new prefab's layout.
    /// Props and lanes need no sync: the game rebuilds them from the prefab when the building updates.
    /// </summary>
    internal static class LotInstanceSync
    {
        /// <param name="netRemap">Old to new cage; null if the old cage is unknown, then nets stay put.</param>
        public static void Sync(EntityManager entityManager, Entity building, Entity oldPrefab, Entity newPrefab,
            float areaMatchMeters, LotRemap netRemap)
        {
            if (oldPrefab == newPrefab || !entityManager.TryGetComponent(building, out Transform transform))
                return;
            try
            {
                int areas = AreaInstanceSync.Sync(entityManager, building, transform, oldPrefab, newPrefab,
                    areaMatchMeters);
                int nets = 0, failed = 0;
                if (netRemap != null)
                    nets = NetInstanceSync.Sync(entityManager, building, transform, netRemap, out failed);
                else
                    Mod.Log.Warn($"Lot sync for building {building}: old cage unknown, lot nets keep their layout.");
                if (areas > 0 || nets > 0)
                    Mod.Log.Info($"Lot sync for building {building}: {areas} areas, {nets} net edges moved" +
                                 (failed > 0 ? $", {failed} net parts could not be remapped." : "."));
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Lot sync for building {building} failed; lot items keep their old layout.");
            }
        }
    }
}
