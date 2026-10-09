using System;
using System.Collections.Generic;
using Unsquare.Core.Housekeeping;
using Unsquare.Core.Persistence;
using Unsquare.Integration.Assets;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Executes <see cref="PrunePlanner"/> decisions: drops expired manifest entries and deletes asset files.
    /// Must run before any variant of this session is restored, so no live prefab loses its geometry.
    /// </summary>
    internal sealed class VariantHousekeeper
    {
        private readonly GeometryAssetStore _store;

        public VariantHousekeeper(GeometryAssetStore store)
        {
            _store = store;
        }

        /// <param name="maxUnusedAge">Variants unused for longer are pruned; null keeps them (orphans are still swept).</param>
        public void Run(VariantManifest manifest, BoundsCache boundsCache, DateTime nowUtc, TimeSpan? maxUnusedAge)
        {
            int prunedEntries = 0, pruned = 0;
            if (maxUnusedAge.HasValue)
            {
                var prune = PrunePlanner.PlanPrune(manifest, nowUtc, maxUnusedAge.Value);
                foreach (var key in prune.EntryKeys)
                    manifest.Remove(key);
                prunedEntries = prune.EntryKeys.Count;
                pruned = DeleteAssets(prune.AssetNames, boundsCache);
            }

            var sweep = PrunePlanner.PlanOrphanSweep(manifest, _store.ListOwnAssetNames());
            if (sweep.IsRefused)
            {
                Mod.Log.Warn($"Orphan sweep skipped: {sweep.Refusal}");
                return;
            }
            int swept = DeleteAssets(sweep.AssetNames, boundsCache);
            Mod.Log.Info($"Housekeeping: pruned {prunedEntries} variants ({pruned} assets), swept {swept} orphans.");
        }

        private int DeleteAssets(IReadOnlyList<string> names, BoundsCache boundsCache)
        {
            if (names.Count == 0)
                return 0;
            foreach (var name in names)
                boundsCache.Remove(name);
            return _store.Delete(new HashSet<string>(names, StringComparer.Ordinal));
        }
    }
}
