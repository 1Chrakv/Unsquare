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
    }
}
