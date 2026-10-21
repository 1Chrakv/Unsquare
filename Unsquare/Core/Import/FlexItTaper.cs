using Unity.Mathematics;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// FlexIt's taper in building space: x shears with depth by an angle blended from the left to the right end,
    /// z with width from back to front. Bilinear in plan, so it holds exactly without FlexIt's end clamp.
    /// </summary>
    public readonly struct FlexItTaper
    {
        private const float MinHalfExtent = 0.001f;

        public FlexItTaper(FlexItVariant variant, float halfWidth, float halfDepth, float pivotZ)
            : this(variant.A, variant.B, variant.C, variant.D, halfWidth, halfDepth, pivotZ)
        {
        }

        public FlexItTaper(float leftDegrees, float rightDegrees, float frontDegrees, float backDegrees,
            float halfWidth, float halfDepth, float pivotZ)
        {
            TanLeft = math.tan(math.radians(leftDegrees));
            TanRight = math.tan(math.radians(rightDegrees));
            TanFront = math.tan(math.radians(frontDegrees));
            TanBack = math.tan(math.radians(backDegrees));
            HalfWidth = math.max(halfWidth, MinHalfExtent);
            HalfDepth = math.max(halfDepth, MinHalfExtent);
            PivotZ = pivotZ;
        }

        public float TanLeft { get; }
        public float TanRight { get; }
        public float TanFront { get; }
        public float TanBack { get; }
        public float HalfWidth { get; }
        public float HalfDepth { get; }

        /// <summary>Depth at which x stays put: FlexIt keeps the street-side edge of the base prefab in place.</summary>
        public float PivotZ { get; }

        public float2 Map(float2 xz)
        {
            float alongX = math.lerp(TanLeft, TanRight, xz.x / (2f * HalfWidth) + 0.5f);
            float alongZ = math.lerp(TanBack, TanFront, xz.y / (2f * HalfDepth) + 0.5f);
            return new float2(xz.x + (xz.y - PivotZ) * alongX, xz.y + xz.x * alongZ);
        }
    }
}
