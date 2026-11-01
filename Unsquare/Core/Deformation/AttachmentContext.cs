using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: the building, lot and blended deformations, the lot
    /// cage box and the building box, whose faces (facades) props align to and whose footprint the blend centres on.
    /// </summary>
    public sealed class AttachmentContext
    {
        public AttachmentContext(IDeformation building, IDeformation lot, IDeformation blend, CageBox box,
            CageBox alignmentBox, AttachmentOptions options)
        {
            Building = building ?? throw new ArgumentNullException(nameof(building));
            Lot = lot ?? throw new ArgumentNullException(nameof(lot));
            Blend = blend ?? throw new ArgumentNullException(nameof(blend));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Box = box;
            AlignmentBox = alignmentBox;
        }

        /// <summary>Deforms what hangs on the building's meshes.</summary>
        public IDeformation Building { get; }

        /// <summary>Deforms ground items away from the building.</summary>
        public IDeformation Lot { get; }

        /// <summary>Building inside the footprint, lot beyond the blend ring: for anything connecting the two.</summary>
        public IDeformation Blend { get; }

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
