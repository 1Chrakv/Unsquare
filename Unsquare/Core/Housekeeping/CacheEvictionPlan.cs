using System.Collections.Generic;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>Assets the geometry cache evicts, and the sizes the decision was based on.</summary>
    public sealed class CacheEvictionPlan
    {
        public CacheEvictionPlan(IReadOnlyList<string> evicted, long cacheBytes, long claimedBytes)
        {
            Evicted = evicted;
            CacheBytes = cacheBytes;
            ClaimedBytes = claimedBytes;
        }

        /// <summary>Least recently used first.</summary>
        public IReadOnlyList<string> Evicted { get; }

        /// <summary>Unclaimed assets before eviction.</summary>
        public long CacheBytes { get; }

        /// <summary>Assets legacy manifest entries claim; outside the cache.</summary>
        public long ClaimedBytes { get; }
    }
}
