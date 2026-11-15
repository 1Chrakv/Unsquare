namespace Unsquare.Api
{
    public enum DeformRequestResult
    {
        Accepted,

        /// <summary>Accepted after clamping offsets to the allowed range.</summary>
        Clamped,

        /// <summary>The cage would fold the building inside out; nothing was requested.</summary>
        Rejected,

        /// <summary>The entity is not a building with deformable geometry.</summary>
        NotDeformable,

        /// <summary>Both cages are fine, but the ground blending between them would fold; nothing was requested.</summary>
        BlendFolds,

        /// <summary>No road or lot edge to align with was found near that side.</summary>
        NoNeighbour,

        /// <summary>The original terrain could not be read; nothing was fitted.</summary>
        TerrainUnavailable,

        /// <summary>The cages are already linked; there is nothing to link.</summary>
        AlreadyLinked,

        /// <summary>The lot is not deformed, so its terrain has nothing to follow.</summary>
        LotUndeformed,
    }
}
