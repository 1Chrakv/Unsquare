using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>A straight piece of a neighbouring lot outline or road edge on the ground, in building space (xz).</summary>
    public readonly struct NeighbourEdge
    {
        public NeighbourEdge(float2 a, float2 b, bool isRoad) : this(a, b, isRoad, float2.zero)
        {
        }

        /// <param name="outward">Unit normal pointing away from the neighbour lot; zero if unknown (roads).</param>
        public NeighbourEdge(float2 a, float2 b, bool isRoad, float2 outward)
        {
            A = a;
            B = b;
            IsRoad = isRoad;
            Outward = outward;
        }

        public float2 A { get; }
        public float2 B { get; }

        /// <summary>Road edges are sampled pieces of a curve, so their end points are no corners to snap to.</summary>
        public bool IsRoad { get; }

        /// <summary>Which way the edge faces, away from its lot; zero if unknown.</summary>
        public float2 Outward { get; }

        public bool HasOutward => math.any(Outward != float2.zero);

        public override string ToString() => $"{(IsRoad ? "road" : "lot")} {A} -> {B}";
    }
}
