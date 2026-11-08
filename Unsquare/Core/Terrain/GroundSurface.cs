using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Height change of the deformed ground plane (building y = 0) above a lot point. A turned cage carries ground
    /// points elsewhere, so the cage is inverted in plan to find the one that now lies over the point.
    /// </summary>
    internal sealed class GroundSurface
    {
        private readonly CageBox _lotBox;
        private readonly TrilinearCageDeformation _deformation;
        private readonly float _groundY;

        public GroundSurface(CageBox lotBox, CageOffsets offsets)
        {
            _lotBox = lotBox;
            _deformation = new TrilinearCageDeformation(lotBox, offsets);
            // A basement puts the box bottom below the ground, so sample the ground plane itself.
            _groundY = math.clamp(0f, lotBox.Min.y, lotBox.Max.y);
        }

        public float LiftAt(float2 xz)
        {
            // Lot points outside the deformed outline take the nearest outline height, never an extrapolated one.
            var source = DeformationInverse.TryInvertAtHeight(_deformation, xz, _groundY, out var rest)
                ? math.clamp(rest, _lotBox.Min.xz, _lotBox.Max.xz)
                : xz;
            return _deformation.Apply(new float3(source.x, _groundY, source.y), out _).y - _groundY;
        }
    }
}
