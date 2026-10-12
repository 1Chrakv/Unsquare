namespace Unsquare.Core.Terrain
{
    /// <summary>Which corner columns follow the terrain when fitting a cage to the ground.</summary>
    public enum GroundFitMode
    {
        /// <summary>The back columns follow the terrain; the street-side columns go to road level (y = 0).</summary>
        BackOnly,

        /// <summary>All four columns follow the terrain.</summary>
        AllCorners,
    }
}
