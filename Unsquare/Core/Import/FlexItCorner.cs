using Unity.Mathematics;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// FlexIt's corner fit in building space: a wing turned rigidly by the angle around a front corner, blended across a
    /// diagonal band into a tapered rest. It folds the plan, so a single cage can only approximate it.
    /// </summary>
    public readonly struct FlexItCorner
    {
        private const float MinExtent = 0.001f;

        private readonly float _sin;
        private readonly float _cos;
        private readonly float _tan;

        public FlexItCorner(float degrees, int side, float halfWidth, float cornerX, float pivotZ, float blendHalf)
        {
            float radians = math.radians(degrees);
            _sin = math.sin(radians);
            _cos = math.cos(radians);
            _tan = math.tan(radians);
            Side = side >= 0 ? 1 : -1;
            HalfWidth = math.max(halfWidth, MinExtent);
            CornerX = cornerX;
            PivotZ = pivotZ;
            BlendHalf = math.max(blendHalf, MinExtent);
        }

        /// <summary>+1: the corner is on the +x side, −1: on the −x side.</summary>
        public int Side { get; }

        public float HalfWidth { get; }
        public float CornerX { get; }
        public float PivotZ { get; }
        public float BlendHalf { get; }

        public float2 Map(float2 xz)
        {
            // Tapered part: the end away from the corner leans back by the angle.
            float tanLeft = Side > 0 ? -_tan : 0f;
            float tanRight = Side > 0 ? 0f : -_tan;
            float lean = math.lerp(tanLeft, tanRight, math.saturate(xz.x / (2f * HalfWidth) + 0.5f));
            var tapered = new float2(xz.x + (xz.y - PivotZ) * lean, xz.y);

            // Turned part: rotated about the corner, then sheared back so its depth lines stay on the street.
            float2 d = new float2(xz.x - CornerX, xz.y - PivotZ);
            var rotated = new float2(_cos * d.x + _sin * d.y, _cos * d.y - _sin * d.x);
            var turned = new float2(rotated.x + CornerX - rotated.y * _tan, rotated.y + PivotZ);

            float band = Side * (xz.x - CornerX) + (PivotZ - xz.y);
            float t = math.saturate(band / (2f * BlendHalf) + 0.5f);
            return math.lerp(tapered, turned, t);
        }
    }
}
