using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Rotation part of a deformation Jacobian (polar decomposition J = R·S): the closest rotation to the local
    /// deformation, used to turn rigid objects that cannot scale or shear.
    /// </summary>
    public static class JacobianRotation
    {
        private const float MinDeterminant = 1e-6f;
        private const float Tolerance = 1e-6f;
        private const int MaxIterations = 10;

        /// <summary>Identity for degenerate or inverted Jacobians (the cage validator rejects those anyway).</summary>
        public static quaternion Extract(float3x3 jacobian)
        {
            if (math.determinant(jacobian) <= MinDeterminant)
                return quaternion.identity;

            // Newton iteration R ← ½(R + R⁻ᵀ) converges quadratically to the orthogonal polar factor.
            float3x3 r = jacobian;
            for (int i = 0; i < MaxIterations; i++)
            {
                float3x3 next = (r + math.transpose(math.inverse(r))) * 0.5f;
                bool converged = MaxAbs(next - r) < Tolerance;
                r = next;
                if (converged)
                    break;
            }
            return math.normalize(new quaternion(r));
        }

        private static float MaxAbs(float3x3 m) =>
            math.cmax(math.max(math.abs(m.c0), math.max(math.abs(m.c1), math.abs(m.c2))));
    }
}
