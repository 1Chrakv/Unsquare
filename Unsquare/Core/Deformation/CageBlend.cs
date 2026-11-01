using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Width of the ring around the building's footprint where building and lot deformations blend. Saved nets are
    /// remapped by rebuilding the old deformation from the variant name, so these values are part of the naming scheme.
    /// </summary>
    public static class CageBlend
    {
        /// <summary>Smallest ring width in metres. Never change it.</summary>
        public const float MarginMeters = 4f;

        /// <summary>Ring width per metre of difference between the cages. Never change it.</summary>
        public const float MarginPerMeter = 2f;

        /// <summary>
        /// Ring width for split cages: grows with their difference, so the blend cannot fold paths when the building
        /// grows over its garden. The difference is measured at the corners of the ring's outer box.
        /// </summary>
        public static float Margin(CageBox lotBox, CageBox footprint, DeformSpec spec)
        {
            if (spec.IsLinked)
                return MarginMeters;
            var difference = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets.Minus(spec.LotOffsets));
            float inner = Width(MaxDifference(difference, footprint));
            return Width(MaxDifference(difference, Grow(footprint, inner)));
        }

        private static float Width(float difference) => math.max(MarginMeters, MarginPerMeter * difference);

        // The displacement of a trilinear map is multilinear, so its largest length on a box is at a corner.
        private static float MaxDifference(IDeformation difference, CageBox box)
        {
            float max = 0f;
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                float3 corner = box.Corner(i);
                max = math.max(max, math.length(difference.Apply(corner, out _) - corner));
            }
            return max;
        }

        private static CageBox Grow(CageBox box, float meters)
        {
            var grow = new float3(meters, 0f, meters);
            return new CageBox(box.Min - grow, box.Max + grow);
        }
    }
}
