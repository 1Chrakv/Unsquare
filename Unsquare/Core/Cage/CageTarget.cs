namespace Unsquare.Core.Cage
{
    /// <summary>Which of a building's two cages an edit moves (the edit mode).</summary>
    public enum CageTarget
    {
        /// <summary>The building cage: meshes and attached props.</summary>
        Building,

        /// <summary>The lot cage: ground props, areas, lanes, nets and the terrain.</summary>
        Lot,

        /// <summary>Both cages by the same change; the building keeps its own shape on top of the lot's.</summary>
        Both,
    }
}
