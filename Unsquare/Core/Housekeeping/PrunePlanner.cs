using System;
using System.Collections.Generic;
using Unsquare.Core.Naming;
using Unsquare.Core.Persistence;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>
    /// Decides which variants and asset files can go. Pure planning: the game layer performs deletions.
    /// Assets are shared between variants, so only names no kept entry references are released.
    /// </summary>
    public static class PrunePlanner
    {
        public static readonly TimeSpan DefaultMaxUnusedAge = TimeSpan.FromDays(14);

        /// <summary>Sweeps deleting more than this many files must stay below <see cref="MaxSweepShare"/>.</summary>
        public const int SweepGuardMinCount = 10;
        public const double MaxSweepShare = 0.5;

        /// <summary>Entries used by no city and unused for longer than <paramref name="maxUnusedAge"/>.</summary>
        public static PrunePlan PlanPrune(VariantManifest manifest, DateTime nowUtc, TimeSpan maxUnusedAge)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            var pruned = new List<string>();
            var released = new HashSet<string>(StringComparer.Ordinal);
            var kept = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
            {
                bool expired = entry.Cities.Count == 0 && nowUtc - entry.LastUsedUtc > maxUnusedAge;
                if (expired)
                    pruned.Add(entry.Key);
                (expired ? released : kept).UnionWith(entry.Assets);
            }
            released.ExceptWith(kept);
            return new PrunePlan(pruned, Sorted(released));
        }

        /// <summary>Own asset files on disk that no manifest entry claims, guarded against mass deletion.</summary>
        public static OrphanSweepPlan PlanOrphanSweep(VariantManifest manifest, IEnumerable<string> existingAssetNames)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            if (existingAssetNames == null)
                throw new ArgumentNullException(nameof(existingAssetNames));

            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in manifest.Entries)
            {
                if (entry.Assets.Count == 0)
                    return OrphanSweepPlan.Refuse($"Entry {entry.Key} lists no assets.");
                claimed.UnionWith(entry.Assets);
            }

            int ownCount = 0;
            var orphans = new HashSet<string>(StringComparer.Ordinal);
            foreach (var name in existingAssetNames)
            {
                if (!AssetNaming.IsOwnAssetName(name))
                    continue;
                ownCount++;
                if (!claimed.Contains(name))
                    orphans.Add(name);
            }

            if (orphans.Count > SweepGuardMinCount && orphans.Count > ownCount * MaxSweepShare)
                return OrphanSweepPlan.Refuse($"Would delete {orphans.Count} of {ownCount} assets.");
            return OrphanSweepPlan.Delete(Sorted(orphans));
        }

        private static List<string> Sorted(HashSet<string> names)
        {
            var list = new List<string>(names);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
