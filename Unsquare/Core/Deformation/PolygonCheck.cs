using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>Validates deformed area outlines; the game's triangulation needs simple polygons.</summary>
    public static class PolygonCheck
    {
        /// <summary>True if the closed polygon has at least 3 points and no two non-adjacent edges touch.</summary>
        public static bool IsSimple(IReadOnlyList<float2> points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            if (n < 3)
                return false;
            for (int i = 0; i < n; i++)
            {
                float2 a = points[i], b = points[(i + 1) % n];
                for (int j = i + 1; j < n; j++)
                {
                    // Adjacent edges share a vertex by definition; skip them (incl. last/first pair).
                    if (j == i + 1 || (i == 0 && j == n - 1))
                        continue;
                    if (SegmentsIntersect(a, b, points[j], points[(j + 1) % n]))
                        return false;
                }
            }
            return true;
        }

        private static bool SegmentsIntersect(float2 p1, float2 p2, float2 q1, float2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
            if (((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f)))
                return true;
            return (d1 == 0f && OnSegment(q1, q2, p1)) || (d2 == 0f && OnSegment(q1, q2, p2)) ||
                   (d3 == 0f && OnSegment(p1, p2, q1)) || (d4 == 0f && OnSegment(p1, p2, q2));
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        private static bool OnSegment(float2 a, float2 b, float2 p) =>
            math.all(p >= math.min(a, b) & p <= math.max(a, b));
    }
}
