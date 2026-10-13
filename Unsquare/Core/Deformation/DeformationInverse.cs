using System;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Inverts a deformation point by point with Newton's method. Valid cages preserve orientation, so the
    /// Jacobian stays invertible and a few steps from the deformed point suffice.
    /// </summary>
    public static class DeformationInverse
    {
        private const int MaxIterations = 20;
        private const float ToleranceMeters = 1e-3f;
        private const float MinDeterminant = 1e-6f;

        /// <summary>Finds the undeformed point that maps onto <paramref name="target"/>.</summary>
        public static bool TryInvert(IDeformation deformation, float3 target, out float3 point)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            point = target;
            for (int i = 0; i < MaxIterations; i++)
            {
                float3 residual = deformation.Apply(point, out var jacobian) - target;
                if (math.lengthsq(residual) <= ToleranceMeters * ToleranceMeters)
                    return true;
                if (!(math.abs(math.determinant(jacobian)) > MinDeterminant))
                    return false;
                point -= math.mul(math.inverse(jacobian), residual);
            }
            return math.lengthsq(deformation.Apply(point, out _) - target) <= ToleranceMeters * ToleranceMeters;
        }

        /// <summary>
        /// Finds the ground position (xz at height <paramref name="y"/>) whose deformed xz is
        /// <paramref name="targetXz"/>; ground items keep their heights, so only xz is solved.
        /// </summary>
        public static bool TryInvertAtHeight(IDeformation deformation, float2 targetXz, float y, out float2 xz)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            xz = targetXz;
            for (int i = 0; i < MaxIterations; i++)
            {
                float2 residual = Residual(deformation, xz, y, targetXz, out var jacobian);
                if (math.lengthsq(residual) <= ToleranceMeters * ToleranceMeters)
                    return true;
                // xz rows of the d/dx and d/dz columns.
                float a = jacobian.c0.x, b = jacobian.c2.x, c = jacobian.c0.z, d = jacobian.c2.z;
                float determinant = a * d - b * c;
                if (!(math.abs(determinant) > MinDeterminant))
                    return false;
                xz -= new float2(d * residual.x - b * residual.y, a * residual.y - c * residual.x) / determinant;
            }
            return math.lengthsq(Residual(deformation, xz, y, targetXz, out _)) <= ToleranceMeters * ToleranceMeters;
        }

        /// <summary>
        /// Finds <paramref name="lambda"/> on the line <c>origin + direction·λ</c> whose deformed point has the
        /// coordinate <paramref name="target"/> along the unit <paramref name="direction"/>.
        /// </summary>
        public static bool TryInvertAlong(IDeformation deformation, float3 origin, float3 direction, float target,
            out float lambda)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            lambda = target - math.dot(direction, origin);
            for (int i = 0; i < MaxIterations; i++)
            {
                float residual = math.dot(direction, deformation.Apply(origin + direction * lambda, out var jacobian)) -
                                 target;
                if (math.abs(residual) <= ToleranceMeters)
                    return true;
                float slope = math.dot(direction, math.mul(jacobian, direction));
                if (!(math.abs(slope) > MinDeterminant))
                    return false;
                lambda -= residual / slope;
            }
            return math.abs(math.dot(direction, deformation.Apply(origin + direction * lambda, out _)) - target) <=
                   ToleranceMeters;
        }

        private static float2 Residual(IDeformation deformation, float2 xz, float y, float2 targetXz,
            out float3x3 jacobian) =>
            deformation.Apply(new float3(xz.x, y, xz.y), out jacobian).xz - targetXz;
    }
}
