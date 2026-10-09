using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>Cubic Bézier in building space (mirrors the game's Bezier4x3 without depending on it).</summary>
    public readonly struct CubicCurve
    {
        public CubicCurve(float3 a, float3 b, float3 c, float3 d)
        {
            A = a;
            B = b;
            C = c;
            D = d;
        }

        public float3 A { get; }
        public float3 B { get; }
        public float3 C { get; }
        public float3 D { get; }

        public CubicCurve Reversed() => new(D, C, B, A);
    }
}
