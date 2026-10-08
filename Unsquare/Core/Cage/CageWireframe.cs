using System;

namespace Unsquare.Core.Cage
{
    /// <summary>The 12 edges of a (deformed) cage in building space, for drawing.</summary>
    public static class CageWireframe
    {
        /// <summary>Fills <paramref name="edges"/> (length 12) in <see cref="CageTopology"/> edge order; no allocations.</summary>
        public static void Fill(CageBox box, CageOffsets offsets, CageSegment[] edges)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (edges == null || edges.Length != CageTopology.EdgeCount)
                throw new ArgumentException($"Expected {CageTopology.EdgeCount} edges.", nameof(edges));

            for (int e = 0; e < CageTopology.EdgeCount; e++)
            {
                var corners = CageTopology.CornersOf(CageHandle.Edge(e));
                edges[e] = new CageSegment(
                    DeformedCage.CornerPosition(box, offsets, corners[0]),
                    DeformedCage.CornerPosition(box, offsets, corners[1]));
            }
        }
    }
}
