using System;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// A grabbable part of the cage: a corner, an edge or a face.
    /// Indices follow <see cref="CageTopology"/>.
    /// </summary>
    public readonly struct CageHandle : IEquatable<CageHandle>
    {
        public CageHandleKind Kind { get; }
        public int Index { get; }

        private CageHandle(CageHandleKind kind, int index)
        {
            if (index < 0 || index >= CageTopology.CountOf(kind))
                throw new ArgumentOutOfRangeException(nameof(index), index, $"Invalid {kind} index.");
            Kind = kind;
            Index = index;
        }

        public static CageHandle Corner(int index) => new(CageHandleKind.Corner, index);
        public static CageHandle Edge(int index) => new(CageHandleKind.Edge, index);
        public static CageHandle Face(int index) => new(CageHandleKind.Face, index);

        public bool Equals(CageHandle other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is CageHandle other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Index;
        public override string ToString() => $"{Kind}{Index}";

        public static bool operator ==(CageHandle a, CageHandle b) => a.Equals(b);
        public static bool operator !=(CageHandle a, CageHandle b) => !a.Equals(b);
    }
}
