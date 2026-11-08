namespace Unsquare.UI.Editing
{
    /// <summary>
    /// Why the latest edit command failed, shown under the tool window. The names are the suffixes of its locale
    /// keys (<c>Unsquare.Problem.*</c> in LocaleEN), so renaming one needs the key renamed too.
    /// </summary>
    public enum EditProblem
    {
        /// <summary>The building or lot cage would fold inside out.</summary>
        CageFolds,

        /// <summary>The ground blending between lot and building would fold.</summary>
        BlendFolds,

        NoNeighbour,
        TerrainUnavailable,
        AlreadyLinked,
        LotUndeformed,

        /// <summary>Terrain lift belongs to the lot and does nothing in Building mode.</summary>
        TerrainOfLot,

        /// <summary>The ground fit mode belongs to the lot; seating the building always uses all corners.</summary>
        GroundFitOfLot,

        NoHandle,
        NothingToUndo,
        NothingToRedo,
        NotDeformable,
    }
}
