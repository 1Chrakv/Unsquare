namespace Unsquare.Integration.Systems
{
    /// <summary>Why a variant is created: an edit, a building in the loaded save, or the legacy manifest.</summary>
    internal enum VariantOrigin
    {
        /// <summary>A cage edit (or level-up carry); the cage is validated.</summary>
        Edit,

        /// <summary>A <see cref="Unsquare.Integration.Saves.SavedCage"/> in the loaded save; never refused.</summary>
        Load,

        /// <summary>A manifest entry restored at preload for saves from before saved cages; never refused.</summary>
        Legacy,
    }
}
