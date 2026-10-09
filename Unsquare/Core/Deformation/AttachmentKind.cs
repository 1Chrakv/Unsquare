namespace Unsquare.Core.Deformation
{
    /// <summary>The kinds of building attachments the deformation can carry along.</summary>
    public enum AttachmentKind
    {
        /// <summary>Sub-objects: props, trees, attached sub-buildings.</summary>
        Props,

        /// <summary>Sub-areas: ground surfaces, lots, hedges.</summary>
        Areas,

        /// <summary>Sub-lanes: pedestrian and car paths, fences.</summary>
        Lanes,

        /// <summary>Sub-nets: paths and small roads on the lot.</summary>
        Nets,
    }
}
