using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Api
{
    /// <summary>
    /// Snapshot of a building's lot cage in building space. <see cref="TargetSpec"/> is what the UI should show:
    /// the latest request if one is in flight, otherwise the applied deformation.
    /// </summary>
    public readonly struct CageState
    {
        public CageState(Entity building, CageBox box, DeformSpec appliedSpec, DeformSpec targetSpec,
            DeformStatus status)
        {
            Building = building;
            Box = box;
            AppliedSpec = appliedSpec;
            TargetSpec = targetSpec;
            Status = status;
        }

        public Entity Building { get; }

        /// <summary>The undeformed lot cuboid.</summary>
        public CageBox Box { get; }

        public DeformSpec AppliedSpec { get; }
        public DeformSpec TargetSpec { get; }
        public DeformStatus Status { get; }

        public CageOffsets AppliedOffsets => AppliedSpec.Offsets;
        public CageOffsets TargetOffsets => TargetSpec.Offsets;

        /// <summary>Whether the target is deformed at all; only then can its terrain be lifted.</summary>
        public bool IsDeformed => !TargetOffsets.IsIdentity;

        /// <summary>Whether the target's lot terrain follows the cage (the default for deformed buildings).</summary>
        public bool TerrainLift => IsDeformed && !TargetSpec.FlatTerrain;

        /// <summary>The target's ground outline leaves the original lot (allowed, but may overlap neighbours).</summary>
        public bool ExceedsLot => LotBounds.ExceedsLot(Box, TargetOffsets);

        public float3 CornerPosition(int corner) => DeformedCage.CornerPosition(Box, TargetOffsets, corner);
        public float3 HandlePosition(CageHandle handle) => DeformedCage.HandlePosition(Box, TargetOffsets, handle);
    }
}
