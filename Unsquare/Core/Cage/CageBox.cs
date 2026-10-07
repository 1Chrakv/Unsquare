using System;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// The undeformed bounding cuboid of a building in building space
    /// (x = width, y = up, z = depth, +z = street side).
    /// </summary>
    public readonly struct CageBox : IEquatable<CageBox>
    {
        public const float MinSize = 0.01f;

        public float3 Min { get; }
        public float3 Max { get; }

        public CageBox(float3 min, float3 max)
        {
            if (math.any(max - min < MinSize))
                throw new ArgumentException($"Box must be larger than {MinSize} m on every axis: {min} .. {max}.");
            Min = min;
            Max = max;
        }

        public float3 Size => Max - Min;
        public float3 Center => (Min + Max) * 0.5f;

        /// <summary>Rest position of a corner.</summary>
        public float3 Corner(int corner) => math.select(Min, Max, CageTopology.CornerSide(corner) == 1);

        /// <summary>Rest position of a handle: the mean of its corners.</summary>
        public float3 HandlePosition(CageHandle handle)
        {
            var corners = CageTopology.CornersOf(handle);
            float3 sum = float3.zero;
            for (int i = 0; i < corners.Count; i++)
                sum += Corner(corners[i]);
            return sum / corners.Count;
        }

        /// <summary>Maps a point to box coordinates (0..1 inside the box, unclamped outside).</summary>
        public float3 Normalize(float3 p) => (p - Min) / Size;

        public bool Equals(CageBox other) => Min.Equals(other.Min) && Max.Equals(other.Max);
        public override bool Equals(object obj) => obj is CageBox other && Equals(other);
        public override int GetHashCode() => (Min.GetHashCode() * 397) ^ Max.GetHashCode();
        public override string ToString() => $"[{Min} .. {Max}]";
    }
}
