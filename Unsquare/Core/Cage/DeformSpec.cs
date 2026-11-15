using System;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Everything that makes a building's variant: the lot cage, the building cage (both on the lot box), whether the
    /// lot terrain stays flat and what blends between the cages. Equal cages are "linked" and behave exactly like the
    /// single cage before the split.
    /// </summary>
    public sealed class DeformSpec : IEquatable<DeformSpec>
    {
        public static readonly DeformSpec Identity = new(CageOffsets.Identity, false);

        /// <summary>A linked spec: building and lot follow the same cage.</summary>
        public DeformSpec(CageOffsets offsets, bool flatTerrain)
            : this(offsets, offsets, flatTerrain)
        {
        }

        public DeformSpec(CageOffsets lotOffsets, CageOffsets buildingOffsets, bool flatTerrain,
            BlendMode blendMode = BlendMode.None)
        {
            LotOffsets = lotOffsets ?? throw new ArgumentNullException(nameof(lotOffsets));
            BuildingOffsets = buildingOffsets ?? throw new ArgumentNullException(nameof(buildingOffsets));
            FlatTerrain = flatTerrain;
            BlendMode = blendMode;
        }

        /// <summary>Deforms the ground items (ground props, areas, lanes, nets) and the terrain lift.</summary>
        public CageOffsets LotOffsets { get; }

        /// <summary>Deforms the building's meshes and attached props; stored on the lot box like the lot cage.</summary>
        public CageOffsets BuildingOffsets { get; }

        /// <summary>Whether the lot terrain keeps the game's flat pad instead of following the cage.</summary>
        public bool FlatTerrain { get; }

        /// <summary>What follows the building's walls on the ground; only matters while the cages differ.</summary>
        /// <remarks>Linked specs keep it for the next split; their variants ignore it (<see cref="ForVariant"/>).</remarks>
        public BlendMode BlendMode { get; }

        public bool IsLinked => LotOffsets.Equals(BuildingOffsets);
        public bool IsIdentity => LotOffsets.IsIdentity && BuildingOffsets.IsIdentity;

        public DeformSpec WithLotOffsets(CageOffsets offsets) => new(offsets, BuildingOffsets, FlatTerrain, BlendMode);
        public DeformSpec WithBuildingOffsets(CageOffsets offsets) => new(LotOffsets, offsets, FlatTerrain, BlendMode);
        public DeformSpec WithFlatTerrain(bool flatTerrain) => new(LotOffsets, BuildingOffsets, flatTerrain, BlendMode);
        public DeformSpec WithBlendMode(BlendMode mode) => new(LotOffsets, BuildingOffsets, FlatTerrain, mode);

        /// <summary>
        /// One spec per shape: undeformed is <see cref="Identity"/> (keeping the blend mode) and an undeformed lot has
        /// no terrain to keep flat.
        /// </summary>
        public DeformSpec Normalized()
        {
            if (IsIdentity)
                return BlendMode == Identity.BlendMode ? Identity : Identity.WithBlendMode(BlendMode);
            bool flat = FlatTerrain && !LotOffsets.IsIdentity;
            return flat == FlatTerrain ? this : new(LotOffsets, BuildingOffsets, flat, BlendMode);
        }

        /// <summary>The spec as its variant holds it: normalized, and linked cages blend nothing (None).</summary>
        public DeformSpec ForVariant()
        {
            var normalized = Normalized();
            return normalized.IsLinked && normalized.BlendMode != BlendMode.None
                ? normalized.WithBlendMode(BlendMode.None).Normalized()
                : normalized;
        }

        public bool Equals(DeformSpec other) =>
            other != null && FlatTerrain == other.FlatTerrain && BlendMode == other.BlendMode &&
            LotOffsets.Equals(other.LotOffsets) && BuildingOffsets.Equals(other.BuildingOffsets);

        public override bool Equals(object obj) => obj is DeformSpec other && Equals(other);

        public override int GetHashCode()
        {
            int hash = (LotOffsets.GetHashCode() * 397) ^ BuildingOffsets.GetHashCode();
            hash = (hash * 397) ^ FlatTerrain.GetHashCode();
            return (hash * 397) ^ (int)BlendMode;
        }

        public override string ToString()
        {
            string cages = IsLinked
                ? LotOffsets.ToString()
                : $"lot {LotOffsets}, building {BuildingOffsets}, blend {BlendMode}";
            return FlatTerrain ? $"{cages} (flat terrain)" : cages;
        }
    }
}
