using System;
using System.Collections.Generic;
using Colossal.Entities;
using Unity.Entities;
using Unsquare.Core.Deformation;
using Transform = Game.Objects.Transform;

namespace Unsquare.Integration.Entities
{
    /// <summary>
    /// After a prefab swap, moves the saved lot entities (areas, nets) of the building and its swapped upgrade
    /// extensions to the new prefab layouts. Props and lanes need no sync: the game rebuilds them on update.
    /// </summary>
    internal static class LotInstanceSync
    {
        /// <param name="netRemap">Old to new cage; null if the old cage is unknown or the building kept its
        /// prefab, then its nets stay put.</param>
        /// <param name="extensions">Swapped extensions with their previous prefab.</param>
        public static void Sync(EntityManager entityManager, Entity building, Entity oldPrefab, Entity newPrefab,
            IReadOnlyList<(ExtensionSwap Swap, Entity OldPrefab)> extensions, float areaMatchMeters,
            LotRemap netRemap)
        {
            if (!entityManager.TryGetComponent(building, out Transform transform))
                return;
            try
            {
                int areas = oldPrefab == newPrefab
                    ? 0
                    : AreaInstanceSync.Sync(entityManager, building, transform, oldPrefab, newPrefab, areaMatchMeters);
                var owners = new List<NetOwner> { new(building, netRemap) };
                foreach (var (swap, oldExtension) in extensions)
                {
                    if (entityManager.TryGetComponent(swap.Extension, out Transform placed))
                        areas += AreaInstanceSync.Sync(entityManager, swap.Extension, placed, oldExtension,
                            swap.TargetPrefab, areaMatchMeters);
                    owners.Add(new NetOwner(swap.Extension, swap.NetRemap));
                }
                if (netRemap == null && oldPrefab != newPrefab)
                    Mod.Log.Warn($"Lot sync for building {building}: old cage unknown, lot nets keep their layout.");
                int nets = NetInstanceSync.Sync(entityManager, transform, owners, out int failed, out int slid);
                if (areas > 0 || nets > 0)
                    Mod.Log.Info($"Lot sync for building {building}: {areas} areas, {nets} net edges moved, " +
                                 $"{slid} road ends slid" +
                                 (extensions.Count > 0 ? $", {extensions.Count} extensions" : "") +
                                 (failed > 0 ? $", {failed} net parts could not be remapped." : "."));
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Lot sync for building {building} failed; lot items keep their old layout.");
            }
        }
    }
}
