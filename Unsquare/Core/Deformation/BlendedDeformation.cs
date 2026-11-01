using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Joins the building and lot deformations: the building's inside the footprint, the lot's beyond the margin,
    /// blended in between (by ground position only), so paths and lanes into the building never tear.
    /// </summary>
    public sealed class BlendedDeformation : IDeformation
    {
        private readonly IDeformation _building;
        private readonly IDeformation _lot;
        private readonly float2 _min;
        private readonly float2 _max;
        private readonly float _margin;

        public BlendedDeformation(IDeformation building, IDeformation lot, CageBox footprint, float margin)
        {
            _building = building;
            _lot = lot;
            _min = footprint.Min.xz;
            _max = footprint.Max.xz;
            _margin = margin;
        }

        /// <summary>Weight of the building deformation at a point: 1 inside the footprint, 0 beyond the margin.</summary>
        public float Weight(float3 p) => Weight(p, out _);

        public float3 Apply(float3 p, out float3x3 jacobian)
        {
            float w = Weight(p, out float3 gradient);
            if (w >= 1f)
                return _building.Apply(p, out jacobian);
            if (w <= 0f)
                return _lot.Apply(p, out jacobian);

            float3 b = _building.Apply(p, out float3x3 jb);
            float3 l = _lot.Apply(p, out float3x3 jl);
            float3 difference = b - l;
            // Product rule: the weight changes across the ring too, which adds (B − L) ⊗ ∇w.
            jacobian = w * jb + (1f - w) * jl + new float3x3(
                difference * gradient.x, difference * gradient.y, difference * gradient.z);
            return l + w * difference;
        }

        private float Weight(float3 p, out float3 gradient)
        {
            gradient = float3.zero;
            float2 below = _min - p.xz, above = p.xz - _max;
            float2 outside = math.max(math.max(below, above), 0f);
            float distance = math.length(outside);
            if (distance <= 0f)
                return 1f;
            if (distance >= _margin)
                return 0f;

            float t = distance / _margin;
            float slope = -6f * t * (1f - t) / _margin; // d(1 − smoothstep)/d(distance)
            float2 direction = outside / distance * math.select(new float2(-1f), new float2(1f), above > 0f);
            gradient = new float3(direction.x, 0f, direction.y) * slope;
            return 1f - t * t * (3f - 2f * t);
        }
    }
}
