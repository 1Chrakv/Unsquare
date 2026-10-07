using System.Collections.Generic;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>Manifest entries to drop and the asset files no remaining entry needs.</summary>
    public sealed class PrunePlan
    {
        public PrunePlan(IReadOnlyList<string> entryKeys, IReadOnlyList<string> assetNames)
        {
            EntryKeys = entryKeys;
            AssetNames = assetNames;
        }

        public IReadOnlyList<string> EntryKeys { get; }
        public IReadOnlyList<string> AssetNames { get; }
        public bool IsEmpty => EntryKeys.Count == 0;
    }
}
