using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>A straight piece of a neighbouring lot outline or road edge on the ground, in building space (xz).</summary>
    public readonly struct NeighbourEdge
    {
        public NeighbourEdge(float2 a, float2 b, bool isRoad)
        {
            A = a;
            B = b;
            IsRoad = isRoad;
        }

        public float2 A { get; }
        public float2 B { get; }

        /// <summary>Road edges are sampled pieces of a curve, so their end points are no corners to snap to.</summary>
        public bool IsRoad { get; }

        public override string ToString() => $"{(IsRoad ? "road" : "lot")} {A} -> {B}";
    }
}
