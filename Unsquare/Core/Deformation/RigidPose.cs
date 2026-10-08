using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>Position and rotation of a rigid object (e.g. a prop) in building space.</summary>
    public readonly struct RigidPose
    {
        public RigidPose(float3 position, quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public float3 Position { get; }
        public quaternion Rotation { get; }
    }
}
