using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Tells where a prop attached to a building mesh really stands. Some assets attach their fences and hedges to
    /// the building although they stand on the lot; those belong to the ground, not to the building cage.
    /// </summary>
    public static class PropAnchor
    {
        /// <summary>How far above or below the lot ground an anchor may sit and still stand on it.</summary>
        public const float GroundToleranceMeters = 0.5f;

        /// <summary>How far outside the footprint an anchor must be, so props at the walls stay with the building.</summary>
        public const float FootprintMarginMeters = 0.5f;

        /// <summary>
        /// True when <paramref name="anchor"/> sits at the lot ground (<paramref name="groundY"/>) and further than
        /// <see cref="FootprintMarginMeters"/> outside <paramref name="footprint"/> (the building's mesh box) in xz.
        /// </summary>
        public static bool StandsOnLot(float groundY, CageBox footprint, float3 anchor)
        {
            if (math.abs(anchor.y - groundY) > GroundToleranceMeters)
                return false;
            float2 outside = math.max(footprint.Min.xz - anchor.xz, anchor.xz - footprint.Max.xz);
            return math.cmax(outside) > FootprintMarginMeters;
        }
    }
}
