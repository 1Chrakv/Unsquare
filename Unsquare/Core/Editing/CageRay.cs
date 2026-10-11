using System;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>A mouse ray in building space; the origin is the camera, the direction is normalized.</summary>
    public readonly struct CageRay
    {
        private const float MinDirectionLength = 1e-6f;

        public CageRay(float3 origin, float3 direction)
        {
            float length = math.length(direction);
            if (!(length > MinDirectionLength))
                throw new ArgumentException("Ray direction must not be zero.", nameof(direction));
            Origin = origin;
            Direction = direction / length;
        }

        public float3 Origin { get; }
        public float3 Direction { get; }

        public float3 PointAt(float distance) => Origin + Direction * distance;
    }
}
