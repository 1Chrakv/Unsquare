using System;
using System.Collections.Generic;

namespace Unsquare.Core.Matching
{
    /// <summary>
    /// Pairs existing instances (e.g. a lot's area entities) with expected prefab items by lowest cost first.
    /// Instances without an acceptable partner stay unmatched and are left untouched.
    /// </summary>
    public static class GreedyMatcher
    {
        /// <param name="cost">Cost of pairing instance i with expected item j; NaN or infinity means "never".</param>
        /// <returns>For each instance the matched expected index, or -1.</returns>
        public static int[] Match(int instanceCount, int expectedCount, Func<int, int, float> cost, float maxCost)
        {
            if (cost == null)
                throw new ArgumentNullException(nameof(cost));
            var pairs = new List<(float Cost, int Instance, int Expected)>();
            for (int i = 0; i < instanceCount; i++)
            for (int j = 0; j < expectedCount; j++)
            {
                float c = cost(i, j);
                if (!float.IsNaN(c) && !float.IsInfinity(c) && c <= maxCost)
                    pairs.Add((c, i, j));
            }
            pairs.Sort((x, y) => x.Cost.CompareTo(y.Cost));

            var result = new int[instanceCount];
            for (int i = 0; i < result.Length; i++)
                result[i] = -1;
            var taken = new bool[expectedCount];
            foreach (var (_, instance, expected) in pairs)
            {
                if (result[instance] >= 0 || taken[expected])
                    continue;
                result[instance] = expected;
                taken[expected] = true;
            }
            return result;
        }
    }
}
