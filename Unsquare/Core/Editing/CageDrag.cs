using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// A drag of one handle. Offsets are always derived from the drag start plus the total motion,
    /// so per-frame changes below the offset grid cannot get lost to rounding.
    /// </summary>
    public sealed class CageDrag
    {
        private readonly CageBox _box;
        private CageOffsets _last;

        /// <param name="box">Rest box of the edited cage; rotations turn its deformed corners.</param>
        public CageDrag(CageHandle handle, CageBox box, CageOffsets startOffsets)
        {
            Handle = handle;
            _box = box;
            StartOffsets = startOffsets ?? throw new ArgumentNullException(nameof(startOffsets));
            _last = startOffsets;
        }

        public CageHandle Handle { get; }
        public CageOffsets StartOffsets { get; }

        public CageOffsets OffsetsFor(HandleMotion motion) => motion.Apply(_box, StartOffsets, Handle);

        /// <param name="totalDelta">Building-space delta since the drag started, in meters.</param>
        public CageOffsets OffsetsFor(float3 totalDelta) => OffsetsFor(HandleMotion.Translate(totalDelta));

        /// <summary>
        /// Like <see cref="OffsetsFor(HandleMotion)"/>, but false while the quantized result equals the last one
        /// returned, so a mouse moving within one grid step does not re-request (and delay) the same bake.
        /// </summary>
        public bool TryAdvance(HandleMotion motion, out CageOffsets offsets)
        {
            offsets = OffsetsFor(motion);
            if (offsets.Equals(_last))
                return false;
            _last = offsets;
            return true;
        }

        public bool TryAdvance(float3 totalDelta, out CageOffsets offsets) =>
            TryAdvance(HandleMotion.Translate(totalDelta), out offsets);
    }
}
