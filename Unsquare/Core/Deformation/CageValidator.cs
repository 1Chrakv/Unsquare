using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>Guards against cages that are too large or fold the building inside out.</summary>
    public static class CageValidator
    {
        /// <summary>Max corner offset per axis, relative to the box size on that axis.</summary>
        public const float MaxOffsetFactor = 2f;

        /// <summary>Lower bound of the offset limit so thin boxes can still be edited.</summary>
        public const float MinOffsetLimitMeters = 5f;

        /// <summary>Smallest allowed local volume ratio (Jacobian determinant).</summary>
        public const float MinVolumeRatio = 0.05f;

        private const int SamplesPerAxis = 3;

        public static float3 OffsetLimit(CageBox box) => math.max(box.Size * MaxOffsetFactor, MinOffsetLimitMeters);

        public static CageValidity Validate(CageBox box, CageOffsets offsets)
        {
            if (!IsWithinLimit(box, offsets))
                return CageValidity.OutOfRange;
            return IsOrientationPreserving(box, offsets) ? CageValidity.Valid : CageValidity.Inverted;
        }

        /// <summary>Clamps every corner offset to <see cref="OffsetLimit"/>.</summary>
        public static CageOffsets Clamp(CageBox box, CageOffsets offsets)
        {
            int3 limit = (int3)math.floor(OffsetLimit(box) / CageOffsets.StepMeters);
            var result = offsets;
            for (int i = 0; i < CageTopology.CornerCount; i++)
                result = result.WithCornerSteps(i, math.clamp(offsets.Steps(i), -limit, limit));
            return result;
        }

        private static bool IsWithinLimit(CageBox box, CageOffsets offsets)
        {
            float3 limit = OffsetLimit(box);
            for (int i = 0; i < CageTopology.CornerCount; i++)
            {
                if (math.any(math.abs(offsets.Meters(i)) > limit + 1e-4f))
                    return false;
            }
            return true;
        }

        // Trilinear determinants are polynomial, so a coarse grid incl. corners catches practical fold-overs.
        private static bool IsOrientationPreserving(CageBox box, CageOffsets offsets)
        {
            var deformation = new TrilinearCageDeformation(box, offsets);
            for (int x = 0; x < SamplesPerAxis; x++)
            for (int y = 0; y < SamplesPerAxis; y++)
            for (int z = 0; z < SamplesPerAxis; z++)
            {
                float3 t = new float3(x, y, z) / (SamplesPerAxis - 1);
                deformation.Apply(math.lerp(box.Min, box.Max, t), out float3x3 jacobian);
                if (math.determinant(jacobian) < MinVolumeRatio)
                    return false;
            }
            return true;
        }
    }
}
