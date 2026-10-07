using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Free-form deformation by an 8-corner cage: each point is displaced by the trilinear blend of the corner
    /// offsets. Points outside the box are extrapolated linearly.
    /// </summary>
    public sealed class TrilinearCageDeformation : IDeformation
    {
        private readonly float3 _min;
        private readonly float3 _invSize;
        private readonly float3[] _offsets = new float3[CageTopology.CornerCount];

        public TrilinearCageDeformation(CageBox box, CageOffsets offsets)
        {
            _min = box.Min;
            _invSize = 1f / box.Size;
            for (int i = 0; i < _offsets.Length; i++)
                _offsets[i] = offsets.Meters(i);
        }

        public float3 Apply(float3 p, out float3x3 jacobian)
        {
            float3 u = (p - _min) * _invSize;
            float3 v = 1f - u;
            float3 displacement = float3.zero;
            float3 dX = float3.zero, dY = float3.zero, dZ = float3.zero;

            for (int i = 0; i < _offsets.Length; i++)
            {
                bool hx = (i & 1) != 0, hy = (i & 2) != 0, hz = (i & 4) != 0;
                float wx = hx ? u.x : v.x, wy = hy ? u.y : v.y, wz = hz ? u.z : v.z;
                float3 d = _offsets[i];
                displacement += wx * wy * wz * d;
                // Partial derivatives of the weight with respect to u; the sign flips on the min side.
                dX += (hx ? 1f : -1f) * wy * wz * d;
                dY += (hy ? 1f : -1f) * wx * wz * d;
                dZ += (hz ? 1f : -1f) * wx * wy * d;
            }

            jacobian = new float3x3(
                new float3(1f, 0f, 0f) + dX * _invSize.x,
                new float3(0f, 1f, 0f) + dY * _invSize.y,
                new float3(0f, 0f, 1f) + dZ * _invSize.z);
            return p + displacement;
        }
    }
}
