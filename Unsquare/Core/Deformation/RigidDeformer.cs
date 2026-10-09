using System;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Moves a rigid object with a deformation: its anchor follows the deformed space, its rotation picks up the
    /// local rotation of the deformation. Shape is kept (objects cannot scale or shear).
    /// </summary>
    public static class RigidDeformer
    {
        public static RigidPose Deform(IDeformation deformation, RigidPose pose)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            float3 position = deformation.Apply(pose.Position, out float3x3 jacobian);
            quaternion rotation = math.normalize(math.mul(JacobianRotation.Extract(jacobian), pose.Rotation));
            return new RigidPose(position, rotation);
        }

        /// <summary>
        /// Like <see cref="Deform"/>, but only turns around Y: for objects standing on the ground (trees, sheds)
        /// that must stay upright when the cage leans.
        /// </summary>
        public static RigidPose DeformUpright(IDeformation deformation, RigidPose pose)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            float3 position = deformation.Apply(pose.Position, out float3x3 jacobian);
            float yaw = YawChange(jacobian, math.rotate(pose.Rotation, new float3(0f, 0f, 1f)));
            if (float.IsNaN(yaw))
                yaw = YawChange(jacobian, math.rotate(pose.Rotation, new float3(1f, 0f, 0f)));
            if (float.IsNaN(yaw))
                yaw = 0f;
            quaternion rotation = math.normalize(math.mul(quaternion.RotateY(yaw), pose.Rotation));
            return new RigidPose(position, rotation);
        }

        // Signed yaw between an axis and its deformed image, both projected to xz; NaN if either is near vertical.
        private static float YawChange(float3x3 jacobian, float3 axis)
        {
            float2 before = axis.xz;
            float2 after = math.mul(jacobian, axis).xz;
            if (math.lengthsq(before) < 1e-6f || math.lengthsq(after) < 1e-6f)
                return float.NaN;
            return math.atan2(after.x, after.y) - math.atan2(before.x, before.y);
        }
    }
}
