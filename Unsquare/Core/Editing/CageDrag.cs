using System;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// A drag of one handle. Offsets are always derived from the drag start plus the total delta,
    /// so per-frame deltas below the offset grid cannot get lost to rounding.
    /// </summary>
    public sealed class CageDrag
    {
        public CageDrag(CageHandle handle, CageOffsets startOffsets)
        {
            Handle = handle;
            StartOffsets = startOffsets ?? throw new ArgumentNullException(nameof(startOffsets));
        }

        public CageHandle Handle { get; }
        public CageOffsets StartOffsets { get; }

        /// <param name="totalDelta">Building-space delta since the drag started, in meters.</param>
        public CageOffsets OffsetsFor(float3 totalDelta) => StartOffsets.Move(Handle, totalDelta);
    }
}
