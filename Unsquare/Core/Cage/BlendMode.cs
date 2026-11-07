namespace Unsquare.Core.Cage
{
    /// <summary>
    /// What follows the building's walls on the ground while the cages differ. Stored in the manifest by value, so
    /// never reorder or renumber.
    /// </summary>
    public enum BlendMode
    {
        /// <summary>Paths, lanes, markers, ground props over the footprint and ground areas blend.</summary>
        All = 0,

        /// <summary>Only paths, lanes and markers blend; gardens and ground props stay with the lot.</summary>
        Paths = 1,

        /// <summary>Nothing blends; paths into the building may break.</summary>
        None = 2,
    }
}
