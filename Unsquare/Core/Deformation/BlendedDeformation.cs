using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Joins the building and lot deformations: the building's inside the footprint; outside, the lot's plus the
    /// change of the nearest wall, faded out across the ring. Never moves anything more than the wall beside it.
    /// </summary>
    public sealed class BlendedDeformation : IDeformation
    {
        private readonly IDeformation _building;
        private readonly IDeformation _lot;
        private readonly float2 _min;
        private readonly float2 _max;
        private readonly BlendRing _ring;

        public BlendedDeformation(IDeformation building, IDeformation lot, CageBox footprint, BlendRing ring)
        {
            _building = building;
            _lot = lot;
            _min = footprint.Min.xz;
            _max = footprint.Max.xz;
            _ring = ring;
        }

        /// <summary>How much of the wall's change a point takes: 1 inside the footprint, 0 beyond the ring.</summary>
        public float Weight(float3 p) => Weight(p, out _);

        public float3 Apply(float3 p, out float3x3 jacobian)
        {
            float w = Weight(p, out float3 gradient);
            if (w >= 1f)
                return _building.Apply(p, out jacobian);
            float3 l = _lot.Apply(p, out float3x3 jl);
            if (w <= 0f)
            {
                jacobian = jl;
                return l;
            }

            // The nearest wall point: the change is taken there, so it never grows with distance.
            var wall = new float3(math.clamp(p.x, _min.x, _max.x), p.y, math.clamp(p.z, _min.y, _max.y));
            float3 change = _building.Apply(wall, out float3x3 jb) - _lot.Apply(wall, out float3x3 jlWall);
            var jChange = jb - jlWall;
            // The wall point only follows p along axes where p is within the footprint.
            if (p.x < _min.x || p.x > _max.x)
                jChange.c0 = float3.zero;
            if (p.z < _min.y || p.z > _max.y)
                jChange.c2 = float3.zero;
            jacobian = jl + w * jChange + new float3x3(change * gradient.x, change * gradient.y, change * gradient.z);
            return l + w * change;
        }

        // Normalised distance outside the footprint (per side relative to its ring width), faded by smoothstep.
        private float Weight(float3 p, out float3 gradient)
        {
            gradient = float3.zero;
            float x = 0f, dx = 0f, z = 0f, dz = 0f;
            if (p.x < _min.x)
                (x, dx) = ((_min.x - p.x) / _ring.MinX, -1f / _ring.MinX);
            else if (p.x > _max.x)
                (x, dx) = ((p.x - _max.x) / _ring.MaxX, 1f / _ring.MaxX);
            if (p.z < _min.y)
                (z, dz) = ((_min.y - p.z) / _ring.MinZ, -1f / _ring.MinZ);
            else if (p.z > _max.y)
                (z, dz) = ((p.z - _max.y) / _ring.MaxZ, 1f / _ring.MaxZ);

            float s = math.sqrt(x * x + z * z);
            if (s <= 0f)
                return 1f;
            if (s >= 1f)
                return 0f;
            float slope = -6f * s * (1f - s); // d(1 − smoothstep)/ds
            gradient = new float3(x * dx, 0f, z * dz) * (slope / s);
            return 1f - s * s * (3f - 2f * s);
        }
    }
}
