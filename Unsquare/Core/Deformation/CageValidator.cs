using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>Guards against cages that are too large or fold the building inside out.</summary>
    public static class CageValidator
    {
        private const int SamplesPerAxis = 3;

        public static float3 OffsetLimit(CageBox box, CageLimits limits) =>
            math.max(box.Size * Checked(limits).MaxOffsetFactor, limits.MinOffsetLimitMeters);

        public static CageValidity Validate(CageBox box, CageOffsets offsets, CageLimits limits)
        {
            if (!IsWithinLimit(box, offsets, limits))
                return CageValidity.OutOfRange;
            return IsOrientationPreserving(box, offsets, limits) ? CageValidity.Valid : CageValidity.Inverted;
        }

        /// <summary>Clamps every corner offset to <see cref="OffsetLimit"/>.</summary>
        public static CageOffsets Clamp(CageBox box, CageOffsets offsets, CageLimits limits)
        {
            int3 limit = (int3)math.floor(OffsetLimit(box, limits) / CageOffsets.StepMeters);
            var result = offsets;
            for (int i = 0; i < CageTopology.CornerCount; i++)
                result = result.WithCornerSteps(i, math.clamp(offsets.Steps(i), -limit, limit));
            return result;
        }

        private static bool IsWithinLimit(CageBox box, CageOffsets offsets, CageLimits limits)
        {
            float3 limit = OffsetLimit(box, limits);
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                if (math.any(math.abs(offsets.Meters(i)) > limit + 1e-4f))
                    return false;
            }
            return true;
        }

        // Trilinear determinants are polynomial, so a coarse grid incl. corners catches practical fold-overs.
        private static bool IsOrientationPreserving(CageBox box, CageOffsets offsets, CageLimits limits)
        {
            var deformation = new TrilinearCageDeformation(box, offsets);
            for (int x = 0; x < SamplesPerAxis; x++)
            for (int y = 0; y < SamplesPerAxis; y++)
            for (int z = 0; z < SamplesPerAxis; z++)
            {
                float3 t = new float3(x, y, z) / (SamplesPerAxis - 1);
                deformation.Apply(math.lerp(box.Min, box.Max, t), out float3x3 jacobian);
                if (math.determinant(jacobian) < limits.MinVolumeRatio)
                    return false;
            }
            return true;
        }

        private static CageLimits Checked(CageLimits limits) =>
            limits ?? throw new ArgumentNullException(nameof(limits));
    }
}
