using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Fits a cage to the terrain: each corner column (bottom corner and the top corner above it) moves up or down so
    /// that its ground point sits on its target height. Column heights and horizontal offsets stay.
    /// </summary>
    public static class GroundFit
    {
        /// <summary>Ground corners (y bit clear), one per column; its top corner is <c>corner | 2</c>.</summary>
        public static readonly IReadOnlyList<int> GroundCorners = new[] { 0, 1, 4, 5 };

        private const int TopBit = 2;
        private const int StreetSideBit = 4;

        /// <summary>Whether a ground corner's column is on the street side (+z).</summary>
        public static bool IsFrontColumn(int groundCorner) => (groundCorner & StreetSideBit) != 0;

        /// <summary>Building-space xz where the ground plane (y = 0) cuts each deformed column.</summary>
        public static float2[] GroundPoints(CageBox box, CageOffsets offsets)
        {
            var points3 = GroundPoints3(box, offsets);
            var points = new float2[points3.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = points3[i].xz;
            return points;
        }

        /// <summary>
        /// Where each deformed column's ground lies in building space, per column in <see cref="GroundCorners"/> order;
        /// y is the ground height after the deformation.
        /// </summary>
        public static float3[] GroundPoints3(CageBox box, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            var points = new float3[GroundCorners.Count];
            for (int i = 0; i < points.Length; i++)
                points[i] = GroundPoint(box, offsets, GroundCorners[i]);
            return points;
        }

        /// <param name="groundHeights">Terrain height per column relative to the building's ground (y = 0).</param>
        /// <param name="mode">With <see cref="GroundFitMode.BackOnly"/> the street side goes to road level instead.</param>
        public static CageOffsets Fit(CageBox box, CageOffsets offsets, IReadOnlyList<float> groundHeights,
            GroundFitMode mode)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            if (groundHeights == null || groundHeights.Count != GroundCorners.Count)
                throw new ArgumentException($"Expected {GroundCorners.Count} ground heights.", nameof(groundHeights));

            var fitted = offsets;
            for (int i = 0; i < GroundCorners.Count; i++)
            {
                int bottom = GroundCorners[i], top = bottom | TopBit;
                float target = mode == GroundFitMode.BackOnly && IsFrontColumn(bottom) ? 0f : groundHeights[i];
                float delta = target - GroundPoint(box, offsets, bottom).y;
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
