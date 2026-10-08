using System;

namespace Unsquare.Core.Editing
{
    /// <summary>Step size for keyboard nudges; all sizes are multiples of the offset grid.</summary>
    public readonly struct NudgeStep : IEquatable<NudgeStep>
    {
        private static readonly float[] Sizes = { 0.1f, 0.5f, 2f };
        private const int DefaultIndex = 1;

        private readonly int _index;

        private NudgeStep(int index)
        {
            _index = index;
        }

        public static NudgeStep Default => new(DefaultIndex);

        public float Meters => Sizes[_index];

        /// <summary>Cycles small → medium → large → small.</summary>
        public NudgeStep Next() => new((_index + 1) % Sizes.Length);

        public bool Equals(NudgeStep other) => _index == other._index;
        public override bool Equals(object obj) => obj is NudgeStep other && Equals(other);
        public override int GetHashCode() => _index;
        public override string ToString() => $"{Meters} m";
    }
}
