using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: what hangs on the building and what lies on the
    /// ground follow, the lot cage box and the building box (facades to align to, blend footprint).
    /// </summary>
    public sealed class AttachmentContext
    {
        public AttachmentContext(IDeformation building, IDeformation ground, IDeformation lot, CageBox box,
            CageBox alignmentBox, float groundY, AttachmentOptions options)
        {
            Building = building ?? throw new ArgumentNullException(nameof(building));
            Ground = ground ?? throw new ArgumentNullException(nameof(ground));
            Lot = lot ?? throw new ArgumentNullException(nameof(lot));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Box = box;
            AlignmentBox = alignmentBox;
            GroundY = groundY;
        }

        /// <summary>Deforms what hangs on the building's meshes.</summary>
        public IDeformation Building { get; }

        /// <summary>
        /// What everything on the ground follows: paths, lanes, props, markers and area nodes (the blend if blending
        /// is on, else the lot cage).
        /// </summary>
        public IDeformation Ground { get; }

        /// <summary>
        /// The plain lot cage (equal to <see cref="Ground"/> without blending). Area outlines fall back to it where
        /// the blend's curve would make their straight edges cross.
        /// </summary>
        public IDeformation Lot { get; }

        /// <summary>The lot cage box.</summary>
        public CageBox Box { get; }

        /// <summary>The building's mesh box: its faces are the facades props align to, its xz the footprint.</summary>
        public CageBox AlignmentBox { get; }

        /// <summary>Height of the lot ground in this frame (0 for the building, lower for a raised extension).</summary>
        public float GroundY { get; }

        public AttachmentOptions Options { get; }
    }
}
