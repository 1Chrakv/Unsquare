namespace Unsquare.Core.Cage
{
    /// <summary>
    /// What follows the building's walls on the ground while the cages differ; None is the default. Stored in the
    /// manifest by value, so never reorder or renumber (1 was a retired paths-only mode).
    /// </summary>
    public enum BlendMode
    {
        /// <summary>Everything on the ground blends: paths, lanes, props, markers and areas.</summary>
        All = 0,

        /// <summary>Nothing blends; paths into the building may break.</summary>
        None = 2,
    }
}
