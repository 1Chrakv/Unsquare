using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>Splits a segment into dash pieces, e.g. for renderers without a usable dashed-line primitive.</summary>
    public static class SegmentDashes
    {
        /// <summary>
        /// Appends dashes from A towards B; the last dash is clipped at B. Zero-length segments add nothing.
        /// The list is not cleared so callers can reuse one buffer per frame.
        /// </summary>
        public static void Split(CageSegment segment, float dashLength, float gapLength, List<CageSegment> output)
        {
            if (dashLength <= 0f)
                throw new ArgumentException("Dash length must be positive.", nameof(dashLength));
            if (gapLength <= 0f)
                throw new ArgumentException("Gap length must be positive.", nameof(gapLength));
            if (output == null)
                throw new ArgumentNullException(nameof(output));

            float length = math.distance(segment.A, segment.B);
            if (length <= 0f)
                return;
            float3 direction = (segment.B - segment.A) / length;
            for (float start = 0f; start < length; start += dashLength + gapLength)
            {
                float end = math.min(start + dashLength, length);
                output.Add(new CageSegment(segment.A + direction * start, segment.A + direction * end));
            }
        }
    }
}
