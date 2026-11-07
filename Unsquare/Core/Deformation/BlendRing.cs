using System;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Width of the blend ring on each side of the building's footprint, in metres: from the wall to where ground
    /// items stop following it. Sides are −x, +x, −z and +z in building space.
    /// </summary>
    public readonly struct BlendRing
    {
        public BlendRing(float minX, float maxX, float minZ, float maxZ)
        {
            if (!(minX > 0f && maxX > 0f && minZ > 0f && maxZ > 0f))
                throw new ArgumentException("Ring widths must be positive.");
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public static BlendRing Uniform(float width) => new(width, width, width, width);

        public override string ToString() => $"ring -x {MinX:0.0}, +x {MaxX:0.0}, -z {MinZ:0.0}, +z {MaxZ:0.0} m";
    }
}
