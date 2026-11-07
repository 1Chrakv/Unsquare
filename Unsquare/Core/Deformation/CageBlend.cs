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
        /// <summary>Ring width in metres where the lot leaves room. Never change it.</summary>
        public const float MarginMeters = 4f;

        /// <summary>Smallest ring width per metre of wall change, so the fade cannot fold. Never change it.</summary>
        public const float MarginPerMeter = 2f;

        // A wall on the lot edge leaves no room; the ring then only needs to avoid a division by zero.
        private const float MinWidthMeters = 0.1f;

        /// <summary>
        /// Ring widths for split cages: ending at the lot edge where there is room, otherwise as narrow as the wall's
        /// change allows without folding. The change is measured at the footprint corners, where the walls are.
        /// </summary>
        public static BlendRing Ring(CageBox lotBox, CageBox footprint, DeformSpec spec)
        {
            if (spec.IsLinked)
                return BlendRing.Uniform(MarginMeters);
            var difference = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets.Minus(spec.LotOffsets));
            float fold = MarginPerMeter * MaxChange(difference, footprint);
            float width = math.max(MarginMeters, fold);
            return new BlendRing(
                Side(width, fold, footprint.Min.x - lotBox.Min.x),
                Side(width, fold, lotBox.Max.x - footprint.Max.x),
                Side(width, fold, footprint.Min.z - lotBox.Min.z),
                Side(width, fold, lotBox.Max.z - footprint.Max.z));
        }

        private static float Side(float width, float fold, float gap) =>
            math.max(MinWidthMeters, math.min(width, math.max(gap, fold)));

        // The displacement of a trilinear map is multilinear, so its largest length on a box is at a corner.
        private static float MaxChange(IDeformation difference, CageBox box)
        {
            float max = 0f;
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                float3 corner = box.Corner(i);
                max = math.max(max, math.length(difference.Apply(corner, out _) - corner));
            }
            return max;
        }
    }
}
