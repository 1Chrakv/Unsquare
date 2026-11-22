using System;
using System.Collections.Generic;
using Unsquare.Core.Housekeeping;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;
using Unsquare.Integration.Assets;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// Startup housekeeping: expires unused legacy manifest entries, then evicts the least recently used geometry
    /// beyond the cache cap. Must run before any variant of this session is created, so no live prefab loses geometry.
    /// </summary>
    internal sealed class VariantHousekeeper
    {
        private const double BytesPerMegabyte = 1024 * 1024;

        private readonly GeometryAssetStore _store;

        public VariantHousekeeper(GeometryAssetStore store)
        {
            _store = store;
        }

        public void Run(VariantManifest manifest, BoundsCache boundsCache, DateTime nowUtc, long capBytes)
        {
            // Pruned entries only stop being restored; their assets fall to the cache (and the archive keeps them).
            var prune = PrunePlanner.PlanPrune(manifest, nowUtc, PrunePlanner.DefaultMaxUnusedAge);
            foreach (var key in prune.EntryKeys)
                manifest.Remove(key);

            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
                claimed.UnionWith(entry.Assets);

            var files = _store.ListOwnAssets();
            var assets = new List<CachedAsset>(files.Count);
            var sizes = new Dictionary<string, long>(files.Count, StringComparer.Ordinal);
            foreach (var file in files)
            {
                var lastUsed = boundsCache.TryGetLastUsed(file.Name, out var touched) ? touched : file.LastUsedUtc;
                assets.Add(new CachedAsset(file.Name, file.Bytes, lastUsed));
                sizes[file.Name] = file.Bytes;
            }

            var plan = CacheEvictionPlanner.Plan(assets, claimed, capBytes);
            var evicted = plan.Evicted;
            long freed = 0;
            var deleted = evicted.Count > 0
                ? _store.Delete(new HashSet<string>(evicted, StringComparer.Ordinal))
                : new List<string>();
            foreach (var name in deleted)
            {
                if (sizes.TryGetValue(name, out long bytes))
                    freed += bytes;
                sizes.Remove(name);
            }
            if (deleted.Count < evicted.Count)
                Mod.Log.Warn($"Cache eviction: {evicted.Count - deleted.Count} of {evicted.Count} assets could not " +
                             "be deleted.");

            // Bounds of assets that are gone (evicted now or deleted by hand) only cost a re-measure if rebaked.
            int forgotten = 0;
            foreach (var name in boundsCache.Names)
            {
                if (AssetNaming.IsOwnAssetName(name) && !sizes.ContainsKey(name) && boundsCache.Remove(name))
                    forgotten++;
            }

            Mod.Log.Info($"Housekeeping: {prune.EntryKeys.Count} legacy variants unused for " +
                         $"{PrunePlanner.DefaultMaxUnusedAge.TotalDays:0} days pruned (Settings: \"Restore shapes for " +
                         $"old saves\" brings them back); evicted {deleted.Count} assets ({Mb(freed)} MB), cache " +
                         $"{Mb(plan.CacheBytes - freed)} of {Mb(capBytes)} MB, legacy-claimed {Mb(plan.ClaimedBytes)} MB, " +
                         $"{forgotten} stale bounds dropped.");
        }

        private static string Mb(long bytes) => (bytes / BytesPerMegabyte).ToString("0");
    }
}
