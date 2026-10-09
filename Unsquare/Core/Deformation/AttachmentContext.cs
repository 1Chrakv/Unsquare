using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: the deformation, which anchors it affects and the
    /// building box whose faces (facades) props align to. Building scope only touches anchors near the box.
    /// </summary>
    public sealed class AttachmentContext
    {
        public AttachmentContext(IDeformation deformation, CageBox box, CageScope scope, CageBox alignmentBox,
            AttachmentOptions options)
        {
            Deformation = deformation ?? throw new ArgumentNullException(nameof(deformation));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Box = box;
            Scope = scope;
            AlignmentBox = alignmentBox;
        }

        public IDeformation Deformation { get; }

        /// <summary>The cage box of the current scope.</summary>
        public CageBox Box { get; }

        public CageScope Scope { get; }

        /// <summary>The building's mesh box in both scopes: its faces are the facades props align to.</summary>
        public CageBox AlignmentBox { get; }

        public AttachmentOptions Options { get; }

        public bool Affects(float3 anchor) =>
            Scope == CageScope.Lot ||
            math.all(anchor >= Box.Min - Options.BuildingMarginMeters & anchor <= Box.Max + Options.BuildingMarginMeters);
    }
}
