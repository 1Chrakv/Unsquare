namespace Unsquare.Core.Import
{
    /// <summary>Deformation kinds of FlexIt variant prefabs, as named by their separator.</summary>
    public enum FlexItMode
    {
        /// <summary>Plan-bilinear shear of the side walls (and front/back); Unsquare can express it as a cage.</summary>
        Taper,

        /// <summary>Wing turned around a corner; curved, so it is reset on import.</summary>
        Corner,

        /// <summary>Circular arc; curved, so it is reset on import.</summary>
        Arch,

        /// <summary>Legacy yaw ramp across a seam; curved, so it is reset on import.</summary>
        Bend,
    }
}
