using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>The handle an editor currently works on. Immutable; navigation wraps within the kind.</summary>
    public readonly struct CageHandleCursor : IEquatable<CageHandleCursor>
    {
        public CageHandleCursor(CageHandleKind kind, int index)
        {
            Kind = kind;
            Index = Wrap(index, CageTopology.CountOf(kind));
        }

        public static CageHandleCursor From(CageHandle handle) => new(handle.Kind, handle.Index);

        public CageHandleKind Kind { get; }
        public int Index { get; }

        public CageHandle Handle => Kind switch
        {
            CageHandleKind.Edge => CageHandle.Edge(Index),
            CageHandleKind.Face => CageHandle.Face(Index),
            _ => CageHandle.Corner(Index),
        };

        /// <summary>Switches the kind, keeping the index if it exists there and clamping it otherwise.</summary>
        public CageHandleCursor WithKind(CageHandleKind kind) =>
            new(kind, Math.Min(Index, CageTopology.CountOf(kind) - 1));

        public CageHandleCursor Next() => new(Kind, Index + 1);
        public CageHandleCursor Previous() => new(Kind, Index - 1);

        public bool Equals(CageHandleCursor other) => Kind == other.Kind && Index == other.Index;
        public override bool Equals(object obj) => obj is CageHandleCursor other && Equals(other);
        public override int GetHashCode() => ((int)Kind * 397) ^ Index;
        public override string ToString() => $"{Kind}{Index}";

        private static int Wrap(int index, int count) => ((index % count) + count) % count;
    }
}
