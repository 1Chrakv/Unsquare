using System;
using Colossal.Entities;
using Unity.Entities;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// After a prefab swap, moves the building's saved lot entities (areas, nets) to the new prefab's layout.
    /// Props and lanes need no sync: the game rebuilds them from the prefab when the building updates.
    /// </summary>
    internal static class LotInstanceSync
    {
        public static void Sync(EntityManager entityManager, Entity building, Entity oldPrefab, Entity newPrefab,
            SyncTolerances tolerances)
        {
            if (oldPrefab == newPrefab || !entityManager.TryGetComponent(building, out Transform transform))
                return;
            try
            {
                int areas = AreaInstanceSync.Sync(entityManager, building, transform, oldPrefab, newPrefab,
                    tolerances.AreaMeters);
                int nets = NetInstanceSync.Sync(entityManager, building, transform, oldPrefab, newPrefab,
                    tolerances.NetMeters);
                if (areas > 0 || nets > 0)
                    Mod.Log.Info($"Lot sync for building {building}: {areas} areas, {nets} net edges moved.");
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Lot sync for building {building} failed; lot items keep their old layout.");
            }
        }
    }
}
