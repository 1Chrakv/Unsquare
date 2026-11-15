namespace Unsquare.Core.Editing
{
    /// <summary>Maps mouse rays onto a constrained handle motion (gizmo axis, horizontal plane or ring).</summary>
    public interface IHandleDrag
    {
        /// <summary>Total motion since the press; false for frames the constraint cannot map.</summary>
        bool TryMotion(CageRay ray, out HandleMotion motion);
    }
}
