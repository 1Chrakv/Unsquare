using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>A straight piece of a neighbouring lot outline or road edge on the ground, in building space xz.</summary>
    public readonly struct NeighbourEdge
    {
        // Ground points come in GroundFit.GroundPoints order (corners 0, 1, 4, 5); this walks them around the lot.
        private static readonly int[] OutlineOrder = { 0, 1, 3, 2 };

        private NeighbourEdge(float2 a, float2 b, float2 outward)
        {
            A = a;
            B = b;
            Outward = outward;
        }

        /// <summary>A piece of a road edge; its side is unknown, so it may face either way.</summary>
        public static NeighbourEdge Road(float2 a, float2 b) => new(a, b, float2.zero);

        /// <param name="outward">Unit normal pointing away from the neighbour lot.</param>
        public static NeighbourEdge LotSide(float2 a, float2 b, float2 outward) => new(a, b, outward);

        /// <summary>Adds the four sides of a lot, each facing away from the lot's centre.</summary>
        /// <param name="groundPoints">The lot's ground corners in <c>GroundFit.GroundPoints</c> order.</param>
        public static void AddLotOutline(IReadOnlyList<float2> groundPoints, List<NeighbourEdge> edges)
        {
            if (groundPoints == null || groundPoints.Count != OutlineOrder.Length)
                throw new ArgumentException($"Expected {OutlineOrder.Length} ground points.", nameof(groundPoints));
            float2 centre = float2.zero;
            for (int i = 0; i < groundPoints.Count; i++)
                centre += groundPoints[i] / groundPoints.Count;
            for (int i = 0; i < OutlineOrder.Length; i++)
            {
                float2 a = groundPoints[OutlineOrder[i]];
                float2 b = groundPoints[OutlineOrder[(i + 1) % OutlineOrder.Length]];
                float2 normal = math.normalizesafe(new float2(b.y - a.y, a.x - b.x));
                if (math.dot(normal, (a + b) * 0.5f - centre) < 0f)
                    normal = -normal;
                edges.Add(LotSide(a, b, normal));
            }
        }

        public float2 A { get; }
        public float2 B { get; }

        /// <summary>Which way the edge faces, away from its lot; zero for roads.</summary>
        public float2 Outward { get; }

        /// <summary>Road edges are sampled pieces of a curve, so their end points are no corners to snap to.</summary>
        public bool IsRoad => math.all(Outward == float2.zero);

        public override string ToString() => $"{(IsRoad ? "road" : "lot")} {A} -> {B}";
    }
}
