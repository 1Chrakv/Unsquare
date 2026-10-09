using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Step size for keyboard nudges: one of three sizes (small, medium, large), all snapped to the offset grid.
    /// Immutable; <see cref="Next"/> and <see cref="WithSizes"/> return new steps.
    /// </summary>
    public sealed class NudgeStep : IEquatable<NudgeStep>
    {
        private const int SizeCount = 3;
        private const int DefaultIndex = 1;

        private readonly float[] _sizes;
        private readonly int _index;

        private NudgeStep(float[] sizes, int index)
        {
            _sizes = sizes;
            _index = index;
        }

        public static NudgeStep Default { get; } = FromSizes(0.1f, 0.5f, 2f);

        /// <summary>Medium step of the given sizes; sizes are sorted and snapped to at least one grid step.</summary>
        public static NudgeStep FromSizes(float small, float medium, float large) =>
            new(Snap(small, medium, large), DefaultIndex);

        public float Meters => _sizes[_index];

        /// <summary>Cycles small → medium → large → small.</summary>
        public NudgeStep Next() => new(_sizes, (_index + 1) % SizeCount);

        /// <summary>Same position (small, medium or large) with new sizes.</summary>
        public NudgeStep WithSizes(float small, float medium, float large) => new(Snap(small, medium, large), _index);

        public bool Equals(NudgeStep other) =>
            other != null && _index == other._index && _sizes[0].Equals(other._sizes[0]) &&
            _sizes[1].Equals(other._sizes[1]) && _sizes[2].Equals(other._sizes[2]);

        public override bool Equals(object obj) => obj is NudgeStep other && Equals(other);
        public override int GetHashCode() => (_index, _sizes[0], _sizes[1], _sizes[2]).GetHashCode();
        public override string ToString() => $"{Meters} m";

        private static float[] Snap(float small, float medium, float large)
        {
            var sizes = new[] { SnapOne(small), SnapOne(medium), SnapOne(large) };
            Array.Sort(sizes);
            return sizes;
        }

        private static float SnapOne(float meters)
        {
            if (float.IsNaN(meters))
                throw new ArgumentException("Step size must be a number.", nameof(meters));
            return math.max(1f, math.round(meters / CageOffsets.StepMeters)) * CageOffsets.StepMeters;
        }
    }
}
