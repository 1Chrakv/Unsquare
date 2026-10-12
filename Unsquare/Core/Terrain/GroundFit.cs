using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Fits a cage to the terrain: each corner column (bottom corner and the top corner above it) moves up or down so
    /// that its ground point sits on the given terrain height. Column heights and horizontal offsets stay.
    /// </summary>
    public static class GroundFit
    {
        /// <summary>Ground corners (y bit clear), one per column; its top corner is <c>corner | 2</c>.</summary>
        public static readonly IReadOnlyList<int> GroundCorners = new[] { 0, 1, 4, 5 };

        private const int TopBit = 2;

        /// <summary>Building-space xz where the ground plane (y = 0) cuts each deformed column.</summary>
        public static float2[] GroundPoints(CageBox box, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            var points = new float2[GroundCorners.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = GroundPoint(box, offsets, GroundCorners[i]).xz;
            return points;
        }

        /// <param name="groundHeights">Terrain height per column relative to the building's ground (y = 0).</param>
        public static CageOffsets Fit(CageBox box, CageOffsets offsets, IReadOnlyList<float> groundHeights)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (groundHeights == null || groundHeights.Count != GroundCorners.Count)
                throw new ArgumentException($"Expected {GroundCorners.Count} ground heights.", nameof(groundHeights));

            var fitted = offsets;
            for (int i = 0; i < GroundCorners.Count; i++)
            {
                int bottom = GroundCorners[i], top = bottom | TopBit;
                float delta = groundHeights[i] - GroundPoint(box, offsets, bottom).y;
                // One shift in grid steps for both corners keeps the column height exact.
                var shift = new int3(0, CageOffsets.Quantize(delta), 0);
                fitted = fitted.WithCornerSteps(bottom, offsets.Steps(bottom) + shift)
                    .WithCornerSteps(top, offsets.Steps(top) + shift);
            }
            return fitted;
        }

        // A basement puts the box bottom below the ground, so the ground plane lies part way up the column.
        private static float3 GroundPoint(CageBox box, CageOffsets offsets, int bottom)
        {
            float t = math.saturate(-box.Min.y / box.Size.y);
            float3 low = DeformedCage.CornerPosition(box, offsets, bottom);
            float3 high = DeformedCage.CornerPosition(box, offsets, bottom | TopBit);
            return math.lerp(low, high, t);
        }
    }
}
