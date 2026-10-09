using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Moves a rigid object with a deformation: its anchor follows the deformed space, its rotation picks up the
    /// local rotation of the deformation. Shape is kept (objects cannot scale or shear).
    /// </summary>
    public static class RigidDeformer
    {
        /// <summary>Full local rotation (polar part of the Jacobian) at the anchor.</summary>
        public static RigidPose Deform(IDeformation deformation, RigidPose pose) =>
            Place(deformation, null, pose, PlacementMode.Follow);

        /// <summary>Like <see cref="Deform"/>, but only the yaw: for objects standing on the ground.</summary>
        public static RigidPose DeformUpright(IDeformation deformation, RigidPose pose) =>
            Place(deformation, null, pose, PlacementMode.Upright);

        /// <summary>
        /// Places a rigid object; near a face of <paramref name="alignmentBox"/> (the building's facades) its
        /// rotation blends into that face's rotation, so props at a wall stay parallel to it.
        /// </summary>
        public static RigidPose Place(IDeformation deformation, CageBox? alignmentBox, RigidPose pose,
            PlacementMode mode)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            float3 position = deformation.Apply(pose.Position, out float3x3 jacobian);

            quaternion rotation = JacobianRotation.Extract(jacobian);
            if (mode == PlacementMode.Upright)
                rotation = quaternion.RotateY(FaceAlignment.YawOf(rotation));

            if (alignmentBox.HasValue &&
                FaceAlignment.TryFindFace(alignmentBox.Value, pose.Position, mode, out int axis, out int side, out float weight))
            {
                var face = FaceAlignment.FaceRotation(deformation, alignmentBox.Value, pose.Position, axis, side, mode);
                rotation = math.slerp(rotation, face, weight);
            }
            return new RigidPose(position, math.normalize(math.mul(rotation, pose.Rotation)));
        }
    }
}
