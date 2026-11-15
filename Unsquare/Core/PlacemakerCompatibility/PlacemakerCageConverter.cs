using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Import;

namespace Unsquare.Core.PlacemakerCompatibility
{
    /// <summary>
    /// Expresses a Placemaker shape as lot cage offsets. Its map is bilinear in plan within its span, so mapping the
    /// lot corners with the extended map is exact there; top and bottom corners move alike and keep their height.
    /// </summary>
    public static class PlacemakerCageConverter
    {
        /// <summary>Exact over the span's depth (the whole lot if <see cref="PlacemakerCage.CoversDepth"/>).</summary>
        public static CageOffsets ToOffsets(CageBox lotBox, PlacemakerCage cage)
        {
            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                float3 rest = lotBox.Corner(i);
                float2 xz = cage.MapUnclamped(rest.xz);
                meters[i] = new float3(xz.x - rest.x, 0f, xz.y - rest.z);
            }
            return CageOffsets.FromMeters(meters);
        }

        /// <summary>
        /// Closest cage to Placemaker's clamped map over <paramref name="samples"/> (plan points); false if they don't
        /// span the plan. <paramref name="maxError"/> is the worst sample miss in metres.
        /// </summary>
        public static bool TryFitOffsets(CageBox lotBox, float2[] samples, PlacemakerCage cage,
            out CageOffsets offsets, out float maxError)
        {
            offsets = null;
            var targets = new float2[samples.Length];
            for (int i = 0; i < samples.Length; i++)
                targets[i] = cage.Map(samples[i]);
            if (!PlanCageFit.TryFit(lotBox, samples, targets, out var plan, out maxError))
                return false;

            var meters = new float3[CageTopology.CornerCount];
            for (int i = 0; i < meters.Length; i++)
            {
                // Cage corner bits: x = 1, y = 2, z = 4; plan corner bits: x = 1, z = 2.
                int planCorner = (i & 1) | ((i & 4) >> 1);
                float3 rest = lotBox.Corner(i);
                meters[i] = new float3(plan[planCorner].x - rest.x, 0f, plan[planCorner].y - rest.z);
            }
            offsets = CageOffsets.FromMeters(meters);
            return true;
        }
    }
}
