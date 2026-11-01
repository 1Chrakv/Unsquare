using Colossal.Mathematics;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Integration.Prefabs.Attachments
{
    /// <summary>Converts between the game's Bezier4x3 and Core's <see cref="CubicCurve"/>.</summary>
    internal static class CurveConversions
    {
        public static CubicCurve ToCore(Bezier4x3 curve) => new(curve.a, curve.b, curve.c, curve.d);

        public static Bezier4x3 ToGame(CubicCurve curve) => new(curve.A, curve.B, curve.C, curve.D);

        /// <summary>
        /// Deforms a lot curve with the blend; ends on a mesh follow the building (when <paramref name="parentMesh"/>
        /// is given). Ground curves keep their heights since the game fits them to the terrain.
        /// </summary>
        public static Bezier4x3 Deform(AttachmentContext context, Bezier4x3 curve, int2 parentMesh,
            bool attachedEndsFollowBuilding)
        {
            bool onGround = parentMesh.x < 0 && parentMesh.y < 0;
            var core = ToCore(curve);
            var deformed = ToGame(attachedEndsFollowBuilding
                ? CurveDeformer.Deform(context.Blend, context.Building, core, parentMesh.x >= 0, parentMesh.y >= 0)
                : CurveDeformer.Deform(context.Blend, core));
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
