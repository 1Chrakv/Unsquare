using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// How far the cage moves the ground (building y = 0) up or down along the lot edges, as lot terrain heights.
    /// Each edge is a cubic through the lifts at its ends and thirds; it is exact where the lift is linear.
    /// </summary>
    public static class GroundLift
    {
        public static LotEdgeLift FromCage(CageBox lotBox, CageOffsets offsets)
        {
            if (offsets.IsIdentity)
                return LotEdgeLift.Zero;

            var surface = new GroundSurface(lotBox, offsets);
            float2 min = lotBox.Min.xz, max = lotBox.Max.xz;
            // The game's lot corners: a = (+x, -z), b = (-x, -z), c = (-x, +z), d = (+x, +z).
            float2 a = new(max.x, min.y), b = min, c = new(min.x, max.y), d = max;
            float liftA = surface.LiftAt(a), liftB = surface.LiftAt(b);
            float liftC = surface.LiftAt(c), liftD = surface.LiftAt(d);
            return new LotEdgeLift(Edge(surface, a, b, liftA, liftB), Edge(surface, b, c, liftB, liftC),
                Edge(surface, c, d, liftC, liftD), Edge(surface, d, a, liftD, liftA));
        }

        // Bézier controls of the cubic that passes through the lifts at a third and two thirds of the edge.
        private static float3 Edge(GroundSurface surface, float2 from, float2 to, float start, float end)
        {
            float first = surface.LiftAt(math.lerp(from, to, 1f / 3f));
            float second = surface.LiftAt(math.lerp(from, to, 2f / 3f));
            float a = 27f * first - 8f * start - end;
            float b = 27f * second - start - 8f * end;
            return new float3(start, (2f * a - b) / 18f, (2f * b - a) / 18f);
        }
    }
}
