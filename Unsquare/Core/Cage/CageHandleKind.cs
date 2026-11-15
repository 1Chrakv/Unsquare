namespace Unsquare.Core.Cage
{
    /// <summary>Which part of the cuboid a handle grabs: 1, 2, 4 or all 8 corners (the whole cage).</summary>
    public enum CageHandleKind
    {
        Corner,
        Edge,
        Face,
        Cage,
    }
}
