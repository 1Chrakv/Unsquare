using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: the building and lot deformations, what paths and
    /// ground items follow, the lot cage box and the building box (facades to align to, footprint of the blend).
    /// </summary>
    public sealed class AttachmentContext
    {
        public AttachmentContext(IDeformation building, IDeformation lot, IDeformation paths, IDeformation ground,
            CageBox box, CageBox alignmentBox, AttachmentOptions options)
        {
            Building = building ?? throw new ArgumentNullException(nameof(building));
            Lot = lot ?? throw new ArgumentNullException(nameof(lot));
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Box = box;
            AlignmentBox = alignmentBox;
        }

        /// <summary>Deforms what hangs on the building's meshes.</summary>
        public IDeformation Building { get; }

        /// <summary>Deforms ground items away from the building.</summary>
        public IDeformation Lot { get; }

        /// <summary>What paths, lanes and markers follow (the blend unless blending is off).</summary>
        public IDeformation Paths { get; }

        /// <summary>What ground props over the footprint and ground area nodes follow (the blend only for All).</summary>
        public IDeformation Ground { get; }

        /// <summary>The lot cage box.</summary>
        public CageBox Box { get; }

        /// <summary>The building's mesh box: its faces are the facades props align to, its xz the footprint.</summary>
        public CageBox AlignmentBox { get; }

        public AttachmentOptions Options { get; }

        /// <summary>Whether a point lies over the building's footprint (xz of <see cref="AlignmentBox"/>).</summary>
        public bool IsInFootprint(float3 p) =>
            math.all(p.xz >= AlignmentBox.Min.xz) && math.all(p.xz <= AlignmentBox.Max.xz);
    }
}
