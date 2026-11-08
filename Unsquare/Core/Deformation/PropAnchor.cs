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
        /// True when the prop's rotated <paramref name="bounds"/> span the lot ground within <paramref name="tolerance"/>.
        /// Pivots may sit anywhere (French lot walls hang from their top); empty bounds test the pivot itself.
        /// </summary>
        public static bool ReachesGround(float groundY, RigidPose pose, MinMaxBounds bounds,
            float tolerance = GroundToleranceMeters)
        {
            float bottom = 0f, top = 0f;
            if (!bounds.IsEmpty)
            {
                (bottom, top) = (float.MaxValue, float.MinValue);
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = new float3((corner & 1) == 0 ? bounds.Min.x : bounds.Max.x,
                        (corner & 2) == 0 ? bounds.Min.y : bounds.Max.y, (corner & 4) == 0 ? bounds.Min.z : bounds.Max.z);
                    float y = math.rotate(pose.Rotation, p).y;
                    (bottom, top) = (math.min(bottom, y), math.max(top, y));
                }
            }
            return pose.Position.y + bottom <= groundY + tolerance && pose.Position.y + top >= groundY - tolerance;
        }

        /// <summary>
        /// True when the prop reaches the lot ground (<paramref name="groundY"/>) and its pivot lies further than
        /// <see cref="FootprintMarginMeters"/> outside <paramref name="footprint"/> (the building's mesh box) in xz.
        /// </summary>
        public static bool StandsOnLot(float groundY, CageBox footprint, RigidPose pose, MinMaxBounds bounds)
        {
            return ReachesGround(groundY, pose, bounds) && OutsideFootprint(footprint, pose.Position.xz);
        }

        /// <summary>
        /// The same test for a single point, e.g. a lane end the asset attached to a mesh: at the lot ground and
        /// further than <see cref="FootprintMarginMeters"/> outside <paramref name="footprint"/>.
        /// </summary>
        public static bool StandsOnLot(float groundY, CageBox footprint, float3 point) =>
            math.abs(point.y - groundY) <= GroundToleranceMeters && OutsideFootprint(footprint, point.xz);

        private static bool OutsideFootprint(CageBox footprint, float2 point) =>
            math.cmax(math.max(footprint.Min.xz - point, point - footprint.Max.xz)) > FootprintMarginMeters;

        /// <summary>
        /// True for a linear prop at the lot ground whose centre lies within <see cref="LotEdgeReachMeters"/> of a
        /// side of <paramref name="lotBox"/> and whose long axis runs along it: boundary fences, whatever the mesh box.
        /// </summary>
        public static bool RunsAlongLotEdge(CageBox lotBox, float groundY, RigidPose pose, MinMaxBounds bounds,
            LinearFootprint footprint)
        {
            if (!ReachesGround(groundY, pose, bounds))
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
