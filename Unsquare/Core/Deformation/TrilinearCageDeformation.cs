using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Free-form deformation by an 8-corner cage: each point is displaced by the trilinear blend of the corner
    /// offsets. Points outside the box are extrapolated linearly.
    /// </summary>
    public sealed class TrilinearCageDeformation : IDeformation
    {
        public TrilinearCageDeformation(CageBox box, CageOffsets offsets)
        {
            Kernel = new CageKernel(box, offsets);
        }

        /// <summary>The same deformation as plain data, for jobs.</summary>
        public CageKernel Kernel { get; }

        public float3 Apply(float3 p, out float3x3 jacobian) => Kernel.Apply(p, out jacobian);
    }
}
