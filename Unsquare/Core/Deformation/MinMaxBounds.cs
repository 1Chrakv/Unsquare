using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>Axis-aligned bounds that may be flat or empty (unlike <see cref="Cage.CageBox"/>).</summary>
    public readonly struct MinMaxBounds
    {
        public static readonly MinMaxBounds Empty = new(new float3(float.MaxValue), new float3(float.MinValue));

        public float3 Min { get; }
        public float3 Max { get; }

        public MinMaxBounds(float3 min, float3 max)
        {
            Min = min;
            Max = max;
        }

        public bool IsEmpty => math.any(Min > Max);

        public MinMaxBounds Encapsulate(float3 p) => new(math.min(Min, p), math.max(Max, p));

        public MinMaxBounds Encapsulate(MinMaxBounds other) =>
            other.IsEmpty ? this : new MinMaxBounds(math.min(Min, other.Min), math.max(Max, other.Max));

        public override string ToString() => IsEmpty ? "[empty]" : $"[{Min} .. {Max}]";
    }
}
