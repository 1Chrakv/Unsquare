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
        /// Deforms a lot curve the way paths follow the cages; ends on a mesh follow the building when
        /// <paramref name="attachedEndsFollowBuilding"/>. Ground curves keep their heights (the game fits them).
        /// </summary>
        public static Bezier4x3 Deform(AttachmentContext context, Bezier4x3 curve, int2 parentMesh,
            bool attachedEndsFollowBuilding)
        {
            bool onGround = parentMesh.x < 0 && parentMesh.y < 0;
            return attachedEndsFollowBuilding
                ? Deform(context, curve, parentMesh.x >= 0, parentMesh.y >= 0, onGround)
                : Deform(context, curve, false, false, onGround);
        }

        /// <summary>
        /// Deforms a lot curve with explicitly attached ends (following the building); the rest follows the ground.
        /// <paramref name="keepHeights"/> restores the original heights of curves the game fits to the terrain.
        /// </summary>
        public static Bezier4x3 Deform(AttachmentContext context, Bezier4x3 curve, bool startAttached,
            bool endAttached, bool keepHeights)
        {
            var core = ToCore(curve);
            var deformed = ToGame(startAttached || endAttached
                ? CurveDeformer.Deform(context.Ground, context.Building, core, startAttached, endAttached)
                : CurveDeformer.Deform(context.Ground, core));
            if (keepHeights)
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
