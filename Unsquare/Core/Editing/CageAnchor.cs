using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>Where floating UI attaches to a (deformed) cage, in building space.</summary>
    public static class CageAnchor
    {
        private const int TopBit = 2; // corner index bit of the max y side

        /// <summary>
        /// Above the roof: the mean of the four top corners in xz, the highest of them in y, so a window placed
        /// there never sinks into a sloped top.
        /// </summary>
        public static float3 Top(CageBox box, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            float3 sum = float3.zero;
            float top = float.MinValue;
            for (int corner = 0; corner < CageTopology.CornerCount; corner++)
            {
                if ((corner & TopBit) == 0)
                    continue;
                float3 position = DeformedCage.CornerPosition(box, offsets, corner);
                sum += position;
                top = math.max(top, position.y);
            }
            return new float3(sum.x / 4f, top, sum.z / 4f);
        }
    }
}
