using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Moves lot items from one cage deformation of a building to another: undo the old one, apply the new one.
    /// A null deformation is the undeformed base prefab. Building space.
    /// </summary>
    public sealed class LotRemap
    {
        /// <summary>Ground items are deformed at the building's ground plane and keep their heights.</summary>
        public const float GroundY = 0f;

        private readonly IDeformation _from;
        private readonly IDeformation _to;

        public LotRemap(IDeformation from, IDeformation to)
        {
            _from = from;
            _to = to;
        }

        /// <summary>False if the old deformation cannot be inverted at the point; it is then returned unchanged.</summary>
        public bool TryRemap(float3 point, bool onGround, out float3 result)
        {
            result = point;
            if (onGround)
            {
                var xz = point.xz;
                if (_from != null && !DeformationInverse.TryInvertAtHeight(_from, point.xz, GroundY, out xz))
                    return false;
                if (_to != null)
                    xz = _to.Apply(new float3(xz.x, GroundY, xz.y), out _).xz;
                result = new float3(xz.x, point.y, xz.y);
                return true;
            }

            var source = point;
            if (_from != null && !DeformationInverse.TryInvert(_from, point, out source))
                return false;
            result = _to == null ? source : _to.Apply(source, out _);
            return true;
        }

        /// <summary>Remaps all control points, as <see cref="CurveDeformer"/> bakes them; unchanged on failure.</summary>
        public bool TryRemap(CubicCurve curve, bool onGround, out CubicCurve result)
        {
            result = curve;
            if (!TryRemap(curve.A, onGround, out var a) || !TryRemap(curve.B, onGround, out var b) ||
                !TryRemap(curve.C, onGround, out var c) || !TryRemap(curve.D, onGround, out var d))
                return false;
            result = new CubicCurve(a, b, c, d);
            return true;
        }
    }
}
