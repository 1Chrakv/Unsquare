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
        /// <summary>Ring width in metres of linked cages, which never blend. Never change it.</summary>
        public const float MarginMeters = 4f;

        /// <summary>Least ring width per metre of outward wall change, so nothing folds. Never change it.</summary>
        public const float MarginPerMeter = 2.5f;

        // A wall on the lot edge leaves no room; the ring then only needs to avoid a division by zero.
        private const float MinWidthMeters = 0.1f;

        /// <summary>
        /// Ring widths for split cages: from the wall to the lot edge, so items there stay with the lot, unless the
        /// wall's outward change needs more room to not fold. Inward and sideways changes only stretch or shear.
        /// </summary>
        public static BlendRing Ring(CageBox lotBox, CageBox footprint, DeformSpec spec)
        {
            if (spec.IsLinked)
                return BlendRing.Uniform(MarginMeters);
            var difference = new TrilinearCageDeformation(lotBox, spec.BuildingOffsets.Minus(spec.LotOffsets));
            var fold = OutwardFold(difference, footprint);
            return new BlendRing(
                Side(fold.x, footprint.Min.x - lotBox.Min.x),
                Side(fold.y, lotBox.Max.x - footprint.Max.x),
                Side(fold.z, footprint.Min.z - lotBox.Min.z),
                Side(fold.w, lotBox.Max.z - footprint.Max.z));
        }

        private static float Side(float fold, float gap) =>
            math.max(MinWidthMeters, math.max(gap, fold));

        // Fold widths per side (−x, +x, −z, +z). The outward change peaks at a corner (multilinear faces); both sides
        // share it there, keeping (ox/rx)² + (oz/rz)² ≤ 1/2.5² where the fade's gradient is diagonal.
        private static float4 OutwardFold(IDeformation difference, CageBox box)
        {
            float4 fold = float4.zero;
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                float3 corner = box.Corner(i);
                float3 change = difference.Apply(corner, out _) - corner;
                int3 side = CageTopology.CornerSide(i);
                float ox = math.max(0f, side.x == 1 ? change.x : -change.x);
                float oz = math.max(0f, side.z == 1 ? change.z : -change.z);
                float x = MarginPerMeter * math.sqrt(ox * (ox + oz));
                float z = MarginPerMeter * math.sqrt(oz * (ox + oz));
                if (side.x == 1)
                    fold.y = math.max(fold.y, x);
                else
                    fold.x = math.max(fold.x, x);
                if (side.z == 1)
                    fold.w = math.max(fold.w, z);
                else
                    fold.z = math.max(fold.z, z);
            }
            return fold;
        }
    }
}
