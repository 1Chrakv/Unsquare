using System;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Everything that makes a building's variant: the lot cage, the building cage (both on the lot box) and whether
    /// the lot terrain stays flat. Equal cages are "linked" and behave exactly like the single cage before the split.
    /// </summary>
    public sealed class DeformSpec : IEquatable<DeformSpec>
    {
        public static readonly DeformSpec Identity = new(CageOffsets.Identity, false);

        /// <summary>A linked spec: building and lot follow the same cage.</summary>
        public DeformSpec(CageOffsets offsets, bool flatTerrain)
            : this(offsets, offsets, flatTerrain)
        {
        }

        public DeformSpec(CageOffsets lotOffsets, CageOffsets buildingOffsets, bool flatTerrain)
        {
            LotOffsets = lotOffsets ?? throw new ArgumentNullException(nameof(lotOffsets));
            BuildingOffsets = buildingOffsets ?? throw new ArgumentNullException(nameof(buildingOffsets));
            FlatTerrain = flatTerrain;
        }

        /// <summary>Deforms the ground items (ground props, areas, lanes, nets) and the terrain lift.</summary>
        public CageOffsets LotOffsets { get; }

        /// <summary>Deforms the building's meshes and attached props; stored on the lot box like the lot cage.</summary>
        public CageOffsets BuildingOffsets { get; }

        /// <summary>Whether the lot terrain keeps the game's flat pad instead of following the cage.</summary>
        public bool FlatTerrain { get; }

        public bool IsLinked => LotOffsets.Equals(BuildingOffsets);
        public bool IsIdentity => LotOffsets.IsIdentity && BuildingOffsets.IsIdentity;

        public DeformSpec WithLotOffsets(CageOffsets offsets) => new(offsets, BuildingOffsets, FlatTerrain);
        public DeformSpec WithBuildingOffsets(CageOffsets offsets) => new(LotOffsets, offsets, FlatTerrain);
        public DeformSpec WithFlatTerrain(bool flatTerrain) => new(LotOffsets, BuildingOffsets, flatTerrain);

        /// <summary>
        /// One spec per shape: undeformed is <see cref="Identity"/>, and an undeformed lot has no terrain to keep flat.
        /// </summary>
        public DeformSpec Normalized()
        {
            if (IsIdentity)
                return Identity;
            return FlatTerrain && LotOffsets.IsIdentity ? WithFlatTerrain(false) : this;
        }

        public bool Equals(DeformSpec other) =>
            other != null && FlatTerrain == other.FlatTerrain && LotOffsets.Equals(other.LotOffsets) &&
            BuildingOffsets.Equals(other.BuildingOffsets);

        public override bool Equals(object obj) => obj is DeformSpec other && Equals(other);

        public override int GetHashCode() =>
            (((LotOffsets.GetHashCode() * 397) ^ BuildingOffsets.GetHashCode()) * 397) ^ FlatTerrain.GetHashCode();

        public override string ToString()
        {
            string cages = IsLinked ? LotOffsets.ToString() : $"lot {LotOffsets}, building {BuildingOffsets}";
            return FlatTerrain ? $"{cages} (flat terrain)" : cages;
        }
    }
}
