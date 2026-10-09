namespace Unsquare.Core.Deformation
{
    /// <summary>How a rigid object turns with the deformation.</summary>
    public enum PlacementMode
    {
        /// <summary>Full rotation, e.g. props attached to the building that tilt with it.</summary>
        Follow,

        /// <summary>Yaw only, e.g. trees and sheds standing on the ground.</summary>
        Upright,
    }
}
