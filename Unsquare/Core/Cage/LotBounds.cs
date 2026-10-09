using System;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Detects lot deformations whose ground outline leaves the original lot rectangle. Zone cells and terrain
    /// flattening stay rectangular, so such lots overlap neighbours; the editor only warns.
    /// </summary>
    public static class LotBounds
    {
        public const float Tolerance = 0.05f;

        /// <summary>
        /// The bottom face is bilinear, so it stays inside the (convex) lot rectangle exactly when its corners do.
        /// </summary>
        public static bool ExceedsLot(CageBox lotBox, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            float2 min = lotBox.Min.xz - Tolerance, max = lotBox.Max.xz + Tolerance;
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                if (CageTopology.CornerSide(corner).y != 0)
                    continue;
                float2 p = DeformedCage.CornerPosition(lotBox, offsets, corner).xz;
                if (math.any(p < min | p > max))
                    return true;
            }
            return false;
        }
    }
}
