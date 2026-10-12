using System;
using Unity.Mathematics;

namespace Unsquare.Core.Terrain
{
    /// <summary>
    /// Height change of the lot terrain, laid out like the game's lot heights: each edge holds its start corner and
    /// its two cubic Bézier controls, and the next edge holds its end corner. Edges run front → right → back → left.
    /// </summary>
    public readonly struct LotEdgeLift : IEquatable<LotEdgeLift>
    {
        /// <summary>Changes below this are ignored; the game also skips lot height changes under 1 cm.</summary>
        public const float MinMeters = 0.01f;

        public static readonly LotEdgeLift Zero = default;

        public LotEdgeLift(float3 front, float3 right, float3 back, float3 left)
        {
            Front = front;
            Right = right;
            Back = back;
            Left = left;
        }

        public float3 Front { get; }
        public float3 Right { get; }
        public float3 Back { get; }
        public float3 Left { get; }

        public bool IsZero
        {
            get
            {
                float3 max = math.max(math.max(math.abs(Front), math.abs(Right)),
                    math.max(math.abs(Back), math.abs(Left)));
                return math.cmax(max) < MinMeters;
            }
        }

        /// <summary>The same lift measured from a base raised by <paramref name="meters"/>.</summary>
        public LotEdgeLift Minus(float meters) =>
            new(Front - meters, Right - meters, Back - meters, Left - meters);

        public bool Equals(LotEdgeLift other) =>
            Front.Equals(other.Front) && Right.Equals(other.Right) && Back.Equals(other.Back) && Left.Equals(other.Left);

        public override bool Equals(object obj) => obj is LotEdgeLift other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Front.GetHashCode();
                hash = hash * 397 ^ Right.GetHashCode();
                hash = hash * 397 ^ Back.GetHashCode();
                return hash * 397 ^ Left.GetHashCode();
            }
        }

        public override string ToString() => $"front {Front}, right {Right}, back {Back}, left {Left}";
    }
}
