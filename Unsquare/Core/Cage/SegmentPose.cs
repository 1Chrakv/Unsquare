using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Placement of a unit mesh along a segment: centred between the ends, local +Y along the segment.
    /// Used to draw segments as cylinders spanning y = -0.5 … 0.5 scaled by <see cref="Length"/>.
    /// </summary>
    public readonly struct SegmentPose
    {
        private const float MinLength = 1e-4f;
        private const float ParallelEpsilon = 1e-6f;

        private SegmentPose(float3 center, quaternion rotation, float length)
        {
            Center = center;
            Rotation = rotation;
            Length = length;
        }

        public float3 Center { get; }
        public quaternion Rotation { get; }
        public float Length { get; }

        /// <summary>False for (nearly) zero-length segments, which have no direction.</summary>
        public static bool TryCreate(float3 a, float3 b, out SegmentPose pose)
        {
            float length = math.distance(a, b);
            if (length < MinLength)
            {
                pose = default;
                return false;
            }
            pose = new SegmentPose((a + b) * 0.5f, FromUp((b - a) / length), length);
            return true;
        }

        // From-to rotation of +Y onto dir; the cross product vanishes for (anti-)parallel directions.
        private static quaternion FromUp(float3 dir)
        {
            var up = new float3(0f, 1f, 0f);
            float3 axis = math.cross(up, dir);
            float axisLength = math.length(axis);
            if (axisLength < ParallelEpsilon)
                return dir.y > 0f ? quaternion.identity : quaternion.RotateX(math.PI);
            float angle = math.acos(math.clamp(math.dot(up, dir), -1f, 1f));
            return quaternion.AxisAngle(axis / axisLength, angle);
        }
    }
}
