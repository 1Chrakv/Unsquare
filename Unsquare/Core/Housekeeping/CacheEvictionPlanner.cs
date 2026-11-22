using System;
using System.Collections.Generic;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>
    /// Decides which geometry assets leave the cache at startup: least recently used first, until the unclaimed
    /// assets fit the cap. Claimed assets (legacy manifest entries) are outside the cache and never evicted.
    /// </summary>
    public static class CacheEvictionPlanner
    {
        public static CacheEvictionPlan Plan(IReadOnlyList<CachedAsset> assets, ISet<string> claimed, long capBytes)
        {
            if (assets == null)
                throw new ArgumentNullException(nameof(assets));
            if (claimed == null)
                throw new ArgumentNullException(nameof(claimed));
            if (capBytes <= 0)
                throw new ArgumentException("Cache cap must be positive.", nameof(capBytes));

            var candidates = new List<CachedAsset>();
            long cacheBytes = 0, claimedBytes = 0;
            foreach (var asset in assets)
            {
                if (claimed.Contains(asset.Name))
                {
                    claimedBytes += asset.Bytes;
                    continue;
                }
                candidates.Add(asset);
                cacheBytes += asset.Bytes;
            }

            var evicted = new List<string>();
            long total = cacheBytes;
            if (total > capBytes)
                candidates.Sort(OldestFirst);
            foreach (var asset in candidates)
            {
                if (total <= capBytes)
                    break;
                evicted.Add(asset.Name);
                total -= asset.Bytes;
            }
            return new CacheEvictionPlan(evicted, cacheBytes, claimedBytes);
        }

        private static int OldestFirst(CachedAsset a, CachedAsset b)
        {
            int byTime = a.LastUsedUtc.CompareTo(b.LastUsedUtc);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.Name, b.Name);
        }
    }
}
