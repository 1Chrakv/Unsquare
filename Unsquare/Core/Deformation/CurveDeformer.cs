using System;

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
    }
}
