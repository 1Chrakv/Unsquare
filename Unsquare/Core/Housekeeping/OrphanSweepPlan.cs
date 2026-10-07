using System;
using System.Collections.Generic;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>Own asset files that no manifest entry claims, or the reason the sweep was refused.</summary>
    public sealed class OrphanSweepPlan
    {
        private OrphanSweepPlan(IReadOnlyList<string> assetNames, string refusal)
        {
            AssetNames = assetNames;
            Refusal = refusal;
        }

        public IReadOnlyList<string> AssetNames { get; }

        /// <summary>Non-null if the safety guard stopped the sweep; nothing must be deleted then.</summary>
        public string Refusal { get; }

        public bool IsRefused => Refusal != null;

        public static OrphanSweepPlan Delete(IReadOnlyList<string> assetNames) => new(assetNames, null);
        public static OrphanSweepPlan Refuse(string reason) => new(Array.Empty<string>(), reason);
    }
}
