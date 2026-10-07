using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>A spatial deformation in building space, pure and thread-agnostic.</summary>
    public interface IDeformation
    {
        /// <summary>Returns the deformed point and the Jacobian d(p')/d(p) at that point.</summary>
        float3 Apply(float3 p, out float3x3 jacobian);
    }
}
