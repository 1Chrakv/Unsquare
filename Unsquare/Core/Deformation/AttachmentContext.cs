using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// What attachment deformers (props, areas, lanes, …) need: the lot deformation, the lot cage box and the
    /// building box whose faces (facades) props align to.
    /// </summary>
    public sealed class AttachmentContext
    {
        public AttachmentContext(IDeformation deformation, CageBox box, CageBox alignmentBox, AttachmentOptions options)
        {
            Deformation = deformation ?? throw new ArgumentNullException(nameof(deformation));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Box = box;
            AlignmentBox = alignmentBox;
        }

        public IDeformation Deformation { get; }

        /// <summary>The lot cage box.</summary>
        public CageBox Box { get; }

        /// <summary>The building's mesh box: its faces are the facades props align to.</summary>
        public CageBox AlignmentBox { get; }

        public AttachmentOptions Options { get; }
    }
}
