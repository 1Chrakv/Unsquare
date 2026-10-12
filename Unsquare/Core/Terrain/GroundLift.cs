using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// How far the cage moves the ground (building y = 0) up or down along the lot edges, as lot terrain heights.
    /// The deformation is linear along each edge, so start corner and Bézier controls describe it exactly.
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
            float a = surface.LiftAt(new float2(max.x, min.y));
            float b = surface.LiftAt(new float2(min.x, min.y));
            float c = surface.LiftAt(new float2(min.x, max.y));
            float d = surface.LiftAt(new float2(max.x, max.y));
            return new LotEdgeLift(Edge(a, b), Edge(b, c), Edge(c, d), Edge(d, a));
        }

        private static float3 Edge(float start, float end) =>
            new(start, math.lerp(start, end, 1f / 3f), math.lerp(start, end, 2f / 3f));
    }
}
