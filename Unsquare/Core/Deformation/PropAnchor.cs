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

        /// <summary>How far from a lot side (either way) a fence's centre may be to count as running along it.</summary>
        public const float LotEdgeReachMeters = 2f;

        /// <summary>Largest angle between a fence's long axis and the lot side it runs along.</summary>
        public const float MaxEdgeAngleDegrees = 15f;

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

        /// <summary>
        /// True for a linear prop at the lot ground whose centre lies within <see cref="LotEdgeReachMeters"/> of a
        /// side of <paramref name="lotBox"/> and whose long axis runs along it: boundary fences, whatever the mesh box.
        /// </summary>
        public static bool RunsAlongLotEdge(CageBox lotBox, float groundY, RigidPose pose, LinearFootprint footprint)
        {
            if (math.abs(pose.Position.y - groundY) > GroundToleranceMeters)
                return false;
            float2 centre = (pose.Position + math.rotate(pose.Rotation, footprint.Centre)).xz;
            float2 axis = math.rotate(pose.Rotation, footprint.Axis).xz;
            float axisLength = math.length(axis);
            if (axisLength < 1e-4f)
                return false;
            axis /= axisLength;
            float minCos = math.cos(math.radians(MaxEdgeAngleDegrees));
            float2 min = lotBox.Min.xz, max = lotBox.Max.xz;
            // Sides x = const run along z (component 1), sides z = const along x (component 0).
            for (int c = 0; c < 2; c++)
            {
                int along = 1 - c;
                if (math.abs(axis[along]) < minCos ||
                    centre[along] < min[along] - LotEdgeReachMeters || centre[along] > max[along] + LotEdgeReachMeters)
                    continue;
                if (math.abs(centre[c] - min[c]) <= LotEdgeReachMeters || math.abs(centre[c] - max[c]) <= LotEdgeReachMeters)
                    return true;
            }
            return false;
        }
    }
}
