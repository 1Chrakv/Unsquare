using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// The trilinear cage deformation as plain data, usable in jobs: box, the eight corner offsets and the frame
    /// shift of a <see cref="TranslatedDeformation"/>. Points outside the box are extrapolated linearly.
    /// </summary>
    public readonly struct CageKernel
    {
        private readonly float3 _min;
        private readonly float3 _invSize;
        private readonly float3 _shift;
        private readonly float3 _d0, _d1, _d2, _d3, _d4, _d5, _d6, _d7;

        public CageKernel(CageBox box, CageOffsets offsets) : this(box, offsets, float3.zero)
        {
        }

        private CageKernel(CageBox box, CageOffsets offsets, float3 shift)
        {
            _min = box.Min;
            _invSize = 1f / box.Size;
            _shift = shift;
            _d0 = offsets.Meters(0);
            _d1 = offsets.Meters(1);
            _d2 = offsets.Meters(2);
            _d3 = offsets.Meters(3);
            _d4 = offsets.Meters(4);
            _d5 = offsets.Meters(5);
            _d6 = offsets.Meters(6);
            _d7 = offsets.Meters(7);
        }

        private CageKernel(in CageKernel source, float3 shift)
        {
            this = source;
            _shift = shift;
        }

        /// <summary>The same cage seen from a frame placed at <paramref name="offset"/> (adds to earlier shifts).</summary>
        public CageKernel Shifted(float3 offset) => new(this, _shift + offset);

        /// <summary>The kernel of a plain or translated trilinear cage; false for anything else (blends).</summary>
        public static bool TryFrom(IDeformation deformation, out CageKernel kernel)
        {
            switch (deformation)
            {
                case TrilinearCageDeformation cage:
                    kernel = cage.Kernel;
                    return true;
                case TranslatedDeformation translated when TryFrom(translated.Inner, out var inner):
                    kernel = inner.Shifted(translated.Offset);
                    return true;
                default:
                    kernel = default;
                    return false;
            }
        }

        /// <summary>Returns the deformed point and the Jacobian d(p')/d(p) at that point.</summary>
        public float3 Apply(float3 p, out float3x3 jacobian)
        {
            float3 q = p + _shift;
            float3 u = (q - _min) * _invSize;
            float3 v = 1f - u;
            float3 displacement = float3.zero;
            float3 dX = float3.zero, dY = float3.zero, dZ = float3.zero;

            // Corner index bits: 1 = max x, 2 = max y, 4 = max z. The derivative's sign flips on the min side.
            Add(_d0, v.x, v.y, v.z, -1f, -1f, -1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d1, u.x, v.y, v.z, 1f, -1f, -1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d2, v.x, u.y, v.z, -1f, 1f, -1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d3, u.x, u.y, v.z, 1f, 1f, -1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d4, v.x, v.y, u.z, -1f, -1f, 1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d5, u.x, v.y, u.z, 1f, -1f, 1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d6, v.x, u.y, u.z, -1f, 1f, 1f, ref displacement, ref dX, ref dY, ref dZ);
            Add(_d7, u.x, u.y, u.z, 1f, 1f, 1f, ref displacement, ref dX, ref dY, ref dZ);

            jacobian = new float3x3(
                new float3(1f, 0f, 0f) + dX * _invSize.x,
                new float3(0f, 1f, 0f) + dY * _invSize.y,
                new float3(0f, 0f, 1f) + dZ * _invSize.z);
            return q + displacement - _shift;
        }

        private static void Add(float3 d, float wx, float wy, float wz, float sx, float sy, float sz,
            ref float3 displacement, ref float3 dX, ref float3 dY, ref float3 dZ)
        {
            displacement += wx * wy * wz * d;
            dX += sx * wy * wz * d;
            dY += sy * wx * wz * d;
            dZ += sz * wx * wy * d;
        }
    }
}
