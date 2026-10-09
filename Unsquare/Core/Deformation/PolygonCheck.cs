using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Validates deformed area outlines. Assets may already touch themselves (holes joined by zero-width bridges,
    /// repeated points), so a deformation is only rejected when it adds new contacts.
    /// </summary>
    public static class PolygonCheck
    {
        private const float MinEdgeLength = 1e-5f;

        /// <summary>True if the closed polygon has at least 3 points and no two non-adjacent edges touch.</summary>
        public static bool IsSimple(IReadOnlyList<float2> points) => points.Count >= 3 && CountContacts(points) == 0;

        /// <summary>True if <paramref name="deformed"/> crosses or touches itself more often than the original.</summary>
        public static bool AddsContacts(IReadOnlyList<float2> original, IReadOnlyList<float2> deformed) =>
            CountContacts(deformed) > CountContacts(original);

        /// <summary>
        /// Number of non-adjacent edge pairs of the closed polygon that cross or touch. Zero-length edges are
        /// ignored, so duplicate points neither count nor break adjacency.
        /// </summary>
        public static int CountContacts(IReadOnlyList<float2> points)
        {
            if (points == null)
                throw new ArgumentNullException(nameof(points));

            var edges = new List<(float2 A, float2 B)>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                float2 a = points[i], b = points[(i + 1) % points.Count];
                if (math.distance(a, b) > MinEdgeLength)
                    edges.Add((a, b));
            }

            int n = edges.Count, contacts = 0;
            for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                // Consecutive edges (incl. last/first) share a vertex by definition.
                if (j == i + 1 || (i == 0 && j == n - 1))
                    continue;
                if (SegmentsTouch(edges[i].A, edges[i].B, edges[j].A, edges[j].B))
                    contacts++;
            }
            return contacts;
        }

        private static bool SegmentsTouch(float2 p1, float2 p2, float2 q1, float2 q2)
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
