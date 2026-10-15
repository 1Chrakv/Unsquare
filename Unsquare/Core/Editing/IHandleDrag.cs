using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>Maps mouse rays onto a constrained handle movement (gizmo axis or horizontal plane).</summary>
    public interface IHandleDrag
    {
        /// <summary>Total building-space delta since the press; false for frames the constraint cannot map.</summary>
        bool TryDelta(CageRay ray, out float3 delta);
    }
}
