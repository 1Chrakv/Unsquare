using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>Placement of a submesh inside the building; converts between submesh-local and building space.</summary>
    public readonly struct SubmeshTransform
    {
        public static readonly SubmeshTransform Identity = new(float3.zero, quaternion.identity);

        public float3 Position { get; }
        public quaternion Rotation { get; }
        private readonly quaternion _inverseRotation;

        public SubmeshTransform(float3 position, quaternion rotation)
        {
            Position = position;
            Rotation = math.normalize(rotation);
            _inverseRotation = math.inverse(Rotation);
        }

        public float3 PointToBuilding(float3 local) => Position + math.rotate(Rotation, local);
        public float3 PointToLocal(float3 building) => math.rotate(_inverseRotation, building - Position);
        public float3 DirectionToBuilding(float3 local) => math.rotate(Rotation, local);
        public float3 DirectionToLocal(float3 building) => math.rotate(_inverseRotation, building);
    }
}
