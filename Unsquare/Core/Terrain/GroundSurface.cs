using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Terrain
{
    /// <summary>How far the cage moves the ground plane (building y = 0) up or down at a point of the lot.</summary>
    internal sealed class GroundSurface
    {
        private readonly TrilinearCageDeformation _deformation;
        private readonly float _groundY;

        public GroundSurface(CageBox lotBox, CageOffsets offsets)
        {
            _deformation = new TrilinearCageDeformation(lotBox, offsets);
            // A basement puts the box bottom below the ground, so sample the ground plane itself.
            _groundY = math.clamp(0f, lotBox.Min.y, lotBox.Max.y);
        }

        public float LiftAt(float2 xz)
        {
            var p = new float3(xz.x, _groundY, xz.y);
            return _deformation.Apply(p, out _).y - p.y;
        }
    }
}
