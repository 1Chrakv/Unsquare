using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>Guards against cages that are too large or fold the building inside out.</summary>
    public static class CageValidator
    {
        private const int SamplesPerAxis = 3;
        private const int BlendSamplesPerAxis = 12;

        /// <summary>
        /// Max corner offset per axis. The vertical one scales with the largest box side, so long, low lots can still
        /// follow a slope (ground fit) that drops more than their height.
        /// </summary>
        public static float3 OffsetLimit(CageBox box, CageLimits limits)
        {
            float3 size = box.Size;
            size.y = math.cmax(size);
            return math.max(size * Checked(limits).MaxOffsetFactor, limits.MinOffsetLimitMeters);
        }

        public static CageValidity Validate(CageBox box, CageOffsets offsets, CageLimits limits)
        {
            if (!IsWithinLimit(box, offsets, limits))
                return CageValidity.OutOfRange;
            return IsOrientationPreserving(box, offsets, limits) ? CageValidity.Valid : CageValidity.Inverted;
        }

        /// <summary>
        /// Validates both cages: the lot cage on the lot box, the building cage on the building's box, and for split
        /// cages the blend between them. Linked specs are validated exactly as a single lot cage.
        /// </summary>
        public static CageValidity ValidateSplit(CageBox lotBox, CageBox buildingBox, DeformSpec spec,
            CageLimits limits)
        {
            var lot = Validate(lotBox, spec.LotOffsets, limits);
            if (lot != CageValidity.Valid || spec.IsLinked)
                return lot;
            var building = Validate(buildingBox, ExtendedCage.Convert(lotBox, spec.BuildingOffsets, buildingBox),
                limits);
            if (building != CageValidity.Valid)
                return building;
            return IsBlendOrientationPreserving(lotBox, buildingBox, spec, limits)
                ? CageValidity.Valid
                : CageValidity.Inverted;
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

        // Paths, lanes and markers lie on the ground, so the blend ring is sampled at ground height.
        private static bool IsBlendOrientationPreserving(CageBox lotBox, CageBox buildingBox, DeformSpec spec,
            CageLimits limits)
        {
            float margin = CageBlend.Margin(lotBox, buildingBox, spec);
            var blend = SplitDeformations.Create(lotBox, buildingBox, spec).Blend;
            float3 grow = new(margin, 0f, margin);
            float3 min = buildingBox.Min - grow, max = buildingBox.Max + grow;
            float groundY = math.clamp(0f, lotBox.Min.y, lotBox.Max.y);
            for (int x = 0; x <= BlendSamplesPerAxis; x++)
            for (int z = 0; z <= BlendSamplesPerAxis; z++)
            {
                float2 t = new float2(x, z) / BlendSamplesPerAxis;
                float2 xz = math.lerp(min.xz, max.xz, t);
                blend.Apply(new float3(xz.x, groundY, xz.y), out float3x3 jacobian);
                if (math.determinant(jacobian) < limits.MinVolumeRatio)
                    return false;
            }
            return true;
        }

        private static CageLimits Checked(CageLimits limits) =>
            limits ?? throw new ArgumentNullException(nameof(limits));
    }
}
