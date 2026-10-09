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
        /// <summary>Tolerance around the building box, e.g. for props sitting on the roof edge.</summary>
        public const float BuildingMargin = 0.5f;

        public AttachmentContext(IDeformation deformation, CageBox box, CageScope scope, CageBox alignmentBox)
        {
            Deformation = deformation ?? throw new ArgumentNullException(nameof(deformation));
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

        public bool Affects(float3 anchor) =>
            Scope == CageScope.Lot ||
            math.all(anchor >= Box.Min - BuildingMargin & anchor <= Box.Max + BuildingMargin);
    }
}
