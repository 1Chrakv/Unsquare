using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// How far a cage deviates from an affine map. Moving vertices reproduces affine maps exactly even on large
    /// triangles; only the twist bends surfaces between them, so it decides whether coarse LODs still fit.
    /// </summary>
    public static class CageTwist
    {
        /// <summary>Largest corner distance from the best affine fit, in meters; also bounds it inside the box.</summary>
        public static float MaxResidualMeters(CageOffsets offsets)
        {
            int count = CageTopology.CornerCount;
            float3 mean = float3.zero, slopeX = float3.zero, slopeY = float3.zero, slopeZ = float3.zero;
            for (int i = 0; i < count; i++)
            {
                float3 d = offsets.Meters(i);
                float3 s = Signs(i);
                mean += d;
                slopeX += s.x * d;
                slopeY += s.y * d;
                slopeZ += s.z * d;
            }
            mean /= count;
            slopeX /= count;
            slopeY /= count;
            slopeZ /= count;

            float max = 0f;
            for (int i = 0; i < count; i++)
            {
                float3 s = Signs(i);
                float3 residual = offsets.Meters(i) - mean - s.x * slopeX - s.y * slopeY - s.z * slopeZ;
                max = math.max(max, math.length(residual));
            }
            return max;
        }

        // Corner index bits are x, y, z (as in TrilinearCageDeformation); -1 on the min side.
        private static float3 Signs(int corner) => new(
            (corner & 1) != 0 ? 1f : -1f, (corner & 2) != 0 ? 1f : -1f, (corner & 4) != 0 ? 1f : -1f);
    }
}
