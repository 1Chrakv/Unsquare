using System;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Everything that makes a building's variant: the cage offsets and whether its lot terrain stays flat.
    /// Lifting the terrain is the default, so <c>false</c> matches variants made before the flag existed.
    /// </summary>
    public sealed class DeformSpec : IEquatable<DeformSpec>
    {
        public static readonly DeformSpec Identity = new(CageOffsets.Identity, false);

        public DeformSpec(CageOffsets offsets, bool flatTerrain)
        {
            Offsets = offsets ?? throw new ArgumentNullException(nameof(offsets));
            FlatTerrain = flatTerrain;
        }

        public CageOffsets Offsets { get; }

        /// <summary>Whether the lot terrain keeps the game's flat pad instead of following the cage.</summary>
        public bool FlatTerrain { get; }

        public DeformSpec WithOffsets(CageOffsets offsets) => new(offsets, FlatTerrain);
        public DeformSpec WithFlatTerrain(bool flatTerrain) => new(Offsets, flatTerrain);

        public bool Equals(DeformSpec other) =>
            other != null && FlatTerrain == other.FlatTerrain && Offsets.Equals(other.Offsets);

        public override bool Equals(object obj) => obj is DeformSpec other && Equals(other);
        public override int GetHashCode() => (Offsets.GetHashCode() * 397) ^ FlatTerrain.GetHashCode();
        public override string ToString() => FlatTerrain ? $"{Offsets} (flat terrain)" : Offsets.ToString();
    }
}
