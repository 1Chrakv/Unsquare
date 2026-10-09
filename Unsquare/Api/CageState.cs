using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Api
{
    /// <summary>
    /// Snapshot of a building's cage in building space for one scope. <see cref="TargetOffsets"/> is what the UI
    /// should draw: the latest request if one is in flight, otherwise the applied offsets.
    /// </summary>
    public readonly struct CageState
    {
        public CageState(Entity building, CageScope scope, CageBox box, CageOffsets appliedOffsets,
            CageOffsets targetOffsets, DeformStatus status)
        {
            Building = building;
            Scope = scope;
            Box = box;
            AppliedOffsets = appliedOffsets;
            TargetOffsets = targetOffsets;
            Status = status;
        }

        public Entity Building { get; }

        /// <summary>The scope this cage describes; Lot falls back to Building for objects without a lot.</summary>
        public CageScope Scope { get; }

        /// <summary>The undeformed bounding cuboid.</summary>
        public CageBox Box { get; }

        public CageOffsets AppliedOffsets { get; }
        public CageOffsets TargetOffsets { get; }
        public DeformStatus Status { get; }

        /// <summary>Lot scope only: the target's ground outline leaves the original lot (allowed, but overlaps).</summary>
        public bool ExceedsLot => Scope == CageScope.Lot && LotBounds.ExceedsLot(Box, TargetOffsets);

        public float3 CornerPosition(int corner) => DeformedCage.CornerPosition(Box, TargetOffsets, corner);
        public float3 HandlePosition(CageHandle handle) => DeformedCage.HandlePosition(Box, TargetOffsets, handle);
    }
}
