using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Rotation of the nearest building facade (a face of the building box), so props at a wall turn exactly with
    /// it instead of with the average deformation. Weight fades from 1 at the face to 0 at <see cref="FalloffMeters"/>.
    /// </summary>
    public static class FaceAlignment
    {
        public const float FalloffMeters = 6f;
        private const float MinLength = 1e-5f;

        /// <summary>
        /// Nearest candidate face of <paramref name="box"/>: vertical faces only for Upright, all faces for Follow.
        /// Anchors outside the box get weight 1; false when the anchor is further inside than the falloff.
        /// </summary>
        public static bool TryFindFace(CageBox box, float3 anchor, PlacementMode mode,
            out int axis, out int side, out float weight)
        {
            axis = -1;
            side = 0;
            float nearest = float.MaxValue;
            for (int a = 0; a < 3; a++)
            {
                if (mode == PlacementMode.Upright && a == 1)
                    continue;
                // Signed distance into the box: negative means the anchor is outside this face.
                float toMin = anchor[a] - box.Min[a], toMax = box.Max[a] - anchor[a];
                if (toMin < nearest)
                    (nearest, axis, side) = (toMin, a, 0);
                if (toMax < nearest)
                    (nearest, axis, side) = (toMax, a, 1);
            }
            weight = nearest <= 0f ? 1f : 1f - math.smoothstep(0f, FalloffMeters, nearest);
            return weight > 0f;
        }

        /// <summary>
        /// Rotation that maps the undeformed face onto the deformed one, evaluated where the anchor projects onto
        /// the face. Upright returns only the yaw of the face's horizontal tangent.
        /// </summary>
        public static quaternion FaceRotation(IDeformation deformation, CageBox box, float3 anchor,
            int axis, int side, PlacementMode mode)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            if (axis < 0 || axis > 2)
                throw new ArgumentOutOfRangeException(nameof(axis));

            float3 onFace = anchor;
            onFace[axis] = side == 0 ? box.Min[axis] : box.Max[axis];
            deformation.Apply(onFace, out float3x3 jacobian);

            // Primary tangent is horizontal on walls (along the facade) and x on top/bottom faces.
            int primary = axis == 0 ? 2 : 0;
            int secondary = axis == 1 ? 2 : 1;
            float3 ea = Unit(primary), eb = Unit(secondary);
            float3 t1 = math.mul(jacobian, ea), t2 = math.mul(jacobian, eb);

            if (mode == PlacementMode.Upright)
                return quaternion.RotateY(YawBetween(ea, t1));

            float3 normal = math.cross(t1, t2);
            if (math.length(t1) < MinLength || math.length(normal) < MinLength)
                return quaternion.identity;
            float3 u1 = math.normalize(t1), n = math.normalize(normal);
            float3 u2 = math.cross(n, u1);
            var deformedFrame = new float3x3(u1, u2, n);
            var restFrame = new float3x3(ea, eb, math.cross(ea, eb));
            return math.normalize(new quaternion(math.mul(deformedFrame, math.transpose(restFrame))));
        }

        /// <summary>Yaw of a rotation, read from where it turns the forward (or right) axis in xz.</summary>
        public static float YawOf(quaternion rotation)
        {
            float3 forward = math.rotate(rotation, new float3(0f, 0f, 1f));
            if (math.lengthsq(forward.xz) > MinLength)
                return math.atan2(forward.x, forward.z);
            float3 right = math.rotate(rotation, new float3(1f, 0f, 0f));
            return math.atan2(right.x, right.z) - math.PI * 0.5f;
        }

        private static float YawBetween(float3 before, float3 after) =>
            math.lengthsq(after.xz) < MinLength
                ? 0f
                : math.atan2(after.x, after.z) - math.atan2(before.x, before.z);

        private static float3 Unit(int axis)
        {
            var v = float3.zero;
            v[axis] = 1f;
            return v;
        }
    }
}
