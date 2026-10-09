using System;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// A complete deformation request: which cage (scope) and how its corners moved.
    /// Identity offsets mean "undeformed" in every scope, so they compare equal regardless of scope.
    /// </summary>
    public readonly struct CageShape : IEquatable<CageShape>
    {
        public static readonly CageShape Undeformed = new(CageScope.Building, CageOffsets.Identity);

        public CageShape(CageScope scope, CageOffsets offsets)
        {
            Scope = scope;
            Offsets = offsets ?? throw new ArgumentNullException(nameof(offsets));
        }

        public CageScope Scope { get; }
        public CageOffsets Offsets { get; }

        public bool IsIdentity => Offsets == null || Offsets.IsIdentity;

        public bool Equals(CageShape other)
        {
            if (IsIdentity || other.IsIdentity)
                return IsIdentity && other.IsIdentity;
            return Scope == other.Scope && Offsets.Equals(other.Offsets);
        }

        public override bool Equals(object obj) => obj is CageShape other && Equals(other);
        public override int GetHashCode() => IsIdentity ? 0 : ((int)Scope * 397) ^ Offsets.GetHashCode();
        public override string ToString() => IsIdentity ? "undeformed" : $"{Scope} [{Offsets}]";
    }
}
