using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Matching
{
    /// <summary>
    /// Ground-plane (xz) distances between existing instances and expected prefab placements. Heights are
    /// ignored because the game projects ground items onto the terrain.
    /// </summary>
    public static class ShapeDistance
    {
        /// <summary>Mean node distance of two closed polygons at their best cyclic alignment; NaN if counts differ.</summary>
        public static float Polygon(IReadOnlyList<float3> actual, IReadOnlyList<float3> expected)
        {
            if (actual == null || expected == null)
                throw new ArgumentNullException(actual == null ? nameof(actual) : nameof(expected));
            int n = actual.Count;
            if (n == 0 || n != expected.Count)
                return float.NaN;

            float best = float.MaxValue;
            for (int offset = 0; offset < n; offset++)
            {
                float sum = 0f;
                for (int i = 0; i < n && sum < best * n; i++)
                    sum += math.distance(actual[i].xz, expected[(i + offset) % n].xz);
                best = math.min(best, sum / n);
            }
            return best;
        }
    }
}
