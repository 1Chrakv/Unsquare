namespace Unsquare.Core.Editing
{
    /// <summary>How a lot face follows its neighbour's edge; see <see cref="NeighbourAlign"/>.</summary>
    public enum AlignMode
    {
        /// <summary>The corner nearer the neighbour stays; the other slides along its edge until parallel.</summary>
        Angle,

        /// <summary>Both corners slide along their edges onto the neighbour's line.</summary>
        Flush,

        /// <summary>The corners take the neighbour edge's end points (roads: like <see cref="Flush"/>).</summary>
        Full,
    }
}
