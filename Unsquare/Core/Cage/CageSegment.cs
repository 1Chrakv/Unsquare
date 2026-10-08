using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>A straight line between two points in building space.</summary>
    public readonly struct CageSegment
    {
        public CageSegment(float3 a, float3 b)
        {
            A = a;
            B = b;
        }

        public float3 A { get; }
        public float3 B { get; }
    }
}
