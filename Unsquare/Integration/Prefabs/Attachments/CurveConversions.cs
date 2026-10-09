using Colossal.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>Converts between the game's Bezier4x3 and Core's <see cref="CubicCurve"/>.</summary>
    internal static class CurveConversions
    {
        public static CubicCurve ToCore(Bezier4x3 curve) => new(curve.a, curve.b, curve.c, curve.d);

        public static Bezier4x3 ToGame(CubicCurve curve) => new(curve.A, curve.B, curve.C, curve.D);

        /// <summary>Deforms a curve; ground curves keep their heights since the game fits them to the terrain.</summary>
        public static Bezier4x3 Deform(AttachmentContext context, Bezier4x3 curve, bool onGround)
        {
            var deformed = ToGame(CurveDeformer.Deform(context.Deformation, ToCore(curve)));
            if (onGround)
            {
                deformed.a.y = curve.a.y;
                deformed.b.y = curve.b.y;
                deformed.c.y = curve.c.y;
                deformed.d.y = curve.d.y;
            }
            return deformed;
        }
    }
}
