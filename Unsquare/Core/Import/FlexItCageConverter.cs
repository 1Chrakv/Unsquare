using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// Expresses a FlexIt taper and shear as lot cage offsets. Both are bilinear in plan and keep heights, so mapping
    /// the 8 lot corners reproduces them exactly (up to the offset grid); top and bottom corners move alike.
    /// </summary>
    public static class FlexItCageConverter
    {
        public static CageOffsets ToOffsets(CageBox lotBox, FlexItTaper? taper, FlexItShear? shear) =>
            ToOffsets(lotBox, CageOffsets.Identity, taper, shear);

        /// <summary>
        /// FlexIt deforms what is rendered, so it acts on the already deformed corners of <paramref name="applied"/>.
        /// Affine shear after a cage is again a cage; a taper is only exact on an undeformed box.
        /// </summary>
        public static CageOffsets ToOffsets(CageBox lotBox, CageOffsets applied, FlexItTaper? taper,
            FlexItShear? shear)
        {
            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                float3 rest = lotBox.Corner(i);
                float3 corner = rest + applied.Meters(i);
                float2 xz = corner.xz;
                if (taper.HasValue)
                    xz = taper.Value.Map(xz);
                if (shear.HasValue)
                    xz = shear.Value.Map(xz);
                meters[i] = new float3(xz.x, corner.y, xz.y) - rest;
            }
            return CageOffsets.FromMeters(meters);
        }

        /// <summary>
        /// Closest cage to a corner fit (plus shear), fitted over <paramref name="samples"/> on the building outline;
        /// false if they don't span the plan. Heights stay; <paramref name="maxError"/> is the worst sample miss.
        /// </summary>
        public static bool TryFitOffsets(CageBox lotBox, CageOffsets applied, float2[] samples, FlexItCorner corner,
            FlexItShear? shear, out CageOffsets offsets, out float maxError)
        {
            offsets = null;
            var targets = new float2[samples.Length];
            var appliedCage = new TrilinearCageDeformation(lotBox, applied);
            for (int i = 0; i < samples.Length; i++)
            {
                // FlexIt acts on what is rendered: the applied cage at ground level.
                float2 xz = appliedCage.Apply(new float3(samples[i].x, lotBox.Min.y, samples[i].y), out _).xz;
                xz = corner.Map(xz);
                targets[i] = shear.HasValue ? shear.Value.Map(xz) : xz;
            }
            if (!PlanCageFit.TryFit(lotBox, samples, targets, out var plan, out maxError))
                return false;

            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                // Cage corner bits: x = 1, y = 2, z = 4; plan corner bits: x = 1, z = 2.
                int planCorner = (i & 1) | ((i & 4) >> 1);
                float3 deformed = lotBox.Corner(i) + applied.Meters(i);
                float2 xz = plan[planCorner];
                meters[i] = new float3(xz.x, deformed.y, xz.y) - lotBox.Corner(i);
            }
            offsets = CageOffsets.FromMeters(meters);
            return true;
        }
    }
}
