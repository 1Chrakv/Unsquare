using System;
using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Deforms a curve by its control points: exact for affine maps, a close fit for the trilinear cage on
    /// short curves such as fences and paths. Keeps the curve count, so node connections stay intact.
    /// </summary>
    public static class CurveDeformer
    {
        public static CubicCurve Deform(IDeformation deformation, CubicCurve curve)
        {
            if (deformation == null)
                throw new ArgumentNullException(nameof(deformation));
            return new CubicCurve(
                deformation.Apply(curve.A, out _),
                deformation.Apply(curve.B, out _),
                deformation.Apply(curve.C, out _),
                deformation.Apply(curve.D, out _));
        }

        /// <summary>
        /// Deforms a curve, but ends attached to a mesh follow <paramref name="attached"/> exactly; the control point
        /// next to such an end moves along with it, so the curve leaves the end in the same direction.
        /// </summary>
        public static CubicCurve Deform(IDeformation deformation, IDeformation attached, CubicCurve curve,
            bool attachedStart, bool attachedEnd)
        {
            var deformed = Deform(deformation, curve);
            if (attached == null || attached == deformation || (!attachedStart && !attachedEnd))
                return deformed;
            float3 a = deformed.A, b = deformed.B, c = deformed.C, d = deformed.D;
            if (attachedStart)
            {
                float3 shift = attached.Apply(curve.A, out _) - a;
                a += shift;
                b += shift;
            }
            if (attachedEnd)
            {
                float3 shift = attached.Apply(curve.D, out _) - d;
                d += shift;
                c += shift;
            }
            return new CubicCurve(a, b, c, d);
        }
    }
}
