using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Api
{
    /// <summary>
    /// Snapshot of a building's cage in building space. <see cref="TargetOffsets"/> is what the UI should
    /// draw: the latest request if one is in flight, otherwise the applied offsets.
    /// </summary>
    public readonly struct CageState
    {
        public CageState(Entity building, CageBox box, CageOffsets appliedOffsets, CageOffsets targetOffsets,
            DeformStatus status)
        {
            Building = building;
            Box = box;
            AppliedOffsets = appliedOffsets;
            TargetOffsets = targetOffsets;
            Status = status;
        }

        public Entity Building { get; }

        /// <summary>The undeformed bounding cuboid.</summary>
        public CageBox Box { get; }

        public CageOffsets AppliedOffsets { get; }
        public CageOffsets TargetOffsets { get; }
        public DeformStatus Status { get; }

        public float3 CornerPosition(int corner) => DeformedCage.CornerPosition(Box, TargetOffsets, corner);
        public float3 HandlePosition(CageHandle handle) => DeformedCage.HandlePosition(Box, TargetOffsets, handle);
    }
}
