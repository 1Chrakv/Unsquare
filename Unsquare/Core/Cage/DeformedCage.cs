using System;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>Positions of the deformed cage in building space, e.g. for drawing handles.</summary>
    public static class DeformedCage
    {
        public static float3 CornerPosition(CageBox box, CageOffsets offsets, int corner) =>
            box.Corner(corner) + offsets.Meters(corner);

        /// <summary>The mean of the handle's deformed corners (corner, edge midpoint or face center).</summary>
        public static float3 HandlePosition(CageBox box, CageOffsets offsets, CageHandle handle)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            var corners = CageTopology.CornersOf(handle);
            float3 sum = float3.zero;
            for (int i = 0; i < corners.Count; i++)
                sum += CornerPosition(box, offsets, corners[i]);
            return sum / corners.Count;
        }
    }
}
