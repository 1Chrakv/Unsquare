using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.PlacemakerCompatibility
{
    /// <summary>
    /// Placemaker's plan-only building shape in building space: a span (x/z, back = min z) whose 4 corners shift in
    /// x/z, blended bilinearly; heights stay. Across the width it extrapolates, in depth it holds the edge shift.
    /// </summary>
    public readonly struct PlacemakerCage
    {
        // Placemaker keeps degenerate spans from dividing by zero the same way.
        private const float MinSpan = 0.001f;

        public PlacemakerCage(float2 min, float2 max, float2 backLeft, float2 backRight, float2 frontLeft,
            float2 frontRight)
        {
            if (!math.all(math.isfinite(min)) || !math.all(math.isfinite(max)))
                throw new ArgumentException("Span must be finite.");
            Min = min;
            Max = max;
            BackLeft = backLeft;
            BackRight = backRight;
            FrontLeft = frontLeft;
            FrontRight = frontRight;
        }

        /// <summary>Span corners: x across the width, y = building-space z (back to front).</summary>
        public float2 Min { get; }
        public float2 Max { get; }

        public float2 BackLeft { get; }
        public float2 BackRight { get; }
        public float2 FrontLeft { get; }
        public float2 FrontRight { get; }

        /// <summary>Where Placemaker puts a ground point (building-space x/z).</summary>
        public float2 Map(float2 xz) => xz + Shift(xz, clampDepth: true);

        /// <summary>The bilinear map extended in depth too; equals <see cref="Map"/> inside the span's depth.</summary>
        public float2 MapUnclamped(float2 xz) => xz + Shift(xz, clampDepth: false);

        /// <summary>True if the lot lies within the span's depth, so <see cref="Map"/> is bilinear over the whole lot.</summary>
        public bool CoversDepth(CageBox lot) => Min.y <= lot.Min.z + 1e-3f && Max.y >= lot.Max.z - 1e-3f;

        /// <summary>
        /// False for shapes no bent row house has: an empty span, a span beside the lot, or shifts that are not finite or
        /// reach past twice the lot's diagonal. Catches Placemaker changing units or frames without notice.
        /// </summary>
        public bool IsPlausible(CageBox lot)
        {
            if (!math.all(Max > Min))
                return false;
            if (Max.x <= lot.Min.x || Min.x >= lot.Max.x || Max.y <= lot.Min.z || Min.y >= lot.Max.z)
                return false;
            float reach = 2f * math.length(lot.Max.xz - lot.Min.xz);
            return IsShift(BackLeft, reach) && IsShift(BackRight, reach) && IsShift(FrontLeft, reach) &&
                   IsShift(FrontRight, reach);
        }

        private static bool IsShift(float2 shift, float reach) =>
            math.all(math.isfinite(shift)) && math.length(shift) <= reach;

        private float2 Shift(float2 xz, bool clampDepth)
        {
            float2 size = math.max(Max - Min, new float2(MinSpan));
            float u = (xz.x - Min.x) / size.x;
            float v = (xz.y - Min.y) / size.y;
            if (clampDepth)
                v = math.saturate(v);
            float2 back = math.lerp(BackLeft, BackRight, u);
            float2 front = math.lerp(FrontLeft, FrontRight, u);
            return math.lerp(back, front, v);
        }

        public override string ToString() =>
            $"span {Min}..{Max}, shifts BL {BackLeft} BR {BackRight} FL {FrontLeft} FR {FrontRight}";
    }
}
