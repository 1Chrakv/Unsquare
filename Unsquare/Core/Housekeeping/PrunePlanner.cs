using System;
using System.Collections.Generic;
using Unsquare.Core.Persistence;

namespace Unsquare.Core.Housekeeping
{
    /// <summary>
    /// Decides which legacy manifest entries expire. Pure planning: the game layer removes them.
    /// Assets are shared between variants, so only names no kept entry references are released (to the cache).
    /// </summary>
    public static class PrunePlanner
    {
        public static readonly TimeSpan DefaultMaxUnusedAge = TimeSpan.FromDays(14);

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

        private static List<string> Sorted(HashSet<string> names)
        {
            var list = new List<string>(names);
            list.Sort(StringComparer.Ordinal);
            return list;
        }
    }
}
