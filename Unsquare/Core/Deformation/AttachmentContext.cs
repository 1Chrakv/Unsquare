using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: the deformation and which anchors it affects.
    /// Building scope only touches anchors in (or near) the box, so the garden doesn't drift.
    /// </summary>
    public sealed class AttachmentContext
    {
        /// <summary>Tolerance around the building box, e.g. for props sitting on the roof edge.</summary>
        public const float BuildingMargin = 0.5f;

        public AttachmentContext(IDeformation deformation, CageBox box, CageScope scope)
        {
            Deformation = deformation ?? throw new ArgumentNullException(nameof(deformation));
            Box = box;
            Scope = scope;
        }

        public IDeformation Deformation { get; }
        public CageBox Box { get; }
        public CageScope Scope { get; }

        public bool Affects(float3 anchor) =>
            Scope == CageScope.Lot ||
            math.all(anchor >= Box.Min - BuildingMargin & anchor <= Box.Max + BuildingMargin);
    }
}
