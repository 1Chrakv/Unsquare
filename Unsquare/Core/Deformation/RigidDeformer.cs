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
        private const float MinPlanLengthSq = 1e-8f;

        /// <summary>Full local rotation (polar part of the Jacobian) at the anchor.</summary>
        public static RigidPose Deform(IDeformation deformation, RigidPose pose) =>
            Place(deformation, null, 0f, pose, PlacementMode.Follow);

        /// <summary>Like <see cref="Deform"/>, but only the yaw: for objects standing on the ground.</summary>
        public static RigidPose DeformUpright(IDeformation deformation, RigidPose pose) =>
            Place(deformation, null, 0f, pose, PlacementMode.Upright);

        /// <summary>
        /// Places a rigid object; near a face of <paramref name="alignmentBox"/> (the building's facades) its
        /// rotation blends into that face's rotation within <paramref name="alignmentReach"/> metres.
        /// </summary>
        public static RigidPose Place(IDeformation deformation, CageBox? alignmentBox, float alignmentReach,
            RigidPose pose, PlacementMode mode)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            float3 position = deformation.Apply(pose.Position, out float3x3 jacobian);

            quaternion rotation = JacobianRotation.Extract(jacobian);
            if (mode == PlacementMode.Upright)
                rotation = quaternion.RotateY(FaceAlignment.YawOf(rotation));

            if (alignmentBox.HasValue &&
                FaceAlignment.TryFindFace(alignmentBox.Value, pose.Position, mode, alignmentReach, out int axis, out int side, out float weight))
            {
                var face = FaceAlignment.FaceRotation(deformation, alignmentBox.Value, pose.Position, axis, side, mode);
                rotation = math.slerp(rotation, face, weight);
            }
            return new RigidPose(position, math.normalize(math.mul(rotation, pose.Rotation)));
        }

        /// <summary>
        /// Places an upright linear prop so its long axis follows the deformed line through it, and centres it on its
        /// deformed span (pivots often sit at one end, which would put a stretched line's whole gap on one side).
        /// </summary>
        public static RigidPose PlaceLinear(IDeformation deformation, RigidPose pose, LinearFootprint footprint)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            float3 restCentre = pose.Position + math.rotate(pose.Rotation, footprint.Centre);
            float3 centre = deformation.Apply(restCentre, out float3x3 jacobian);
            float3 axis = math.rotate(pose.Rotation, footprint.Axis);
            float3 deformedAxis = math.mul(jacobian, axis);
            if (math.lengthsq(axis.xz) < MinPlanLengthSq || math.lengthsq(deformedAxis.xz) < MinPlanLengthSq)
                return Place(deformation, null, 0f, pose, PlacementMode.Upright);

            var turn = quaternion.RotateY(FaceAlignment.YawBetween(axis, deformedAxis));
            var rotation = math.normalize(math.mul(turn, pose.Rotation));
            return new RigidPose(centre - math.rotate(rotation, footprint.Centre), rotation);
        }
    }
}
