using System;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>One own geometry asset file, as the cache eviction sees it.</summary>
    public readonly struct CachedAsset
    {
        public CachedAsset(string name, long bytes, DateTime lastUsedUtc)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Asset name required.", nameof(name));
            if (bytes < 0)
                throw new ArgumentException("Size must not be negative.", nameof(bytes));
            Name = name;
            Bytes = bytes;
            LastUsedUtc = lastUsedUtc;
        }

        public string Name { get; }
        public long Bytes { get; }

        /// <summary>From the bounds cache, else the file's write time.</summary>
        public DateTime LastUsedUtc { get; }
    }
}
