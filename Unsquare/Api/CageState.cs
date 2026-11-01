using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Api
{
    /// <summary>
    /// Snapshot of one of a building's cages in building space, as an edit target sees it. <see cref="Box"/> and the
    /// offsets are the view of that cage (what is drawn and picked); the specs hold both cages on the lot box.
    /// The target values are the latest request if one is in flight, otherwise the applied deformation.
    /// </summary>
    public readonly struct CageState
    {
        public CageState(Entity building, CageTarget target, CageBox box, CageOffsets appliedOffsets,
            CageOffsets targetOffsets, DeformSpec appliedSpec, DeformSpec targetSpec, DeformStatus status,
            CageBox lotBox, CageBox ghostBox, CageOffsets ghostOffsets, bool hasOwnBuildingBox, bool isOffGround)
        {
            Building = building;
            Target = target;
            Box = box;
            AppliedOffsets = appliedOffsets;
            TargetOffsets = targetOffsets;
            AppliedSpec = appliedSpec;
            TargetSpec = targetSpec;
            Status = status;
            LotBox = lotBox;
            GhostBox = ghostBox;
            GhostOffsets = ghostOffsets;
            HasOwnBuildingBox = hasOwnBuildingBox;
            IsOffGround = isOffGround;
        }

        public Entity Building { get; }

        /// <summary>Which cage this snapshot shows (the edit mode it was asked for).</summary>
        public CageTarget Target { get; }

        /// <summary>
        /// The undeformed box of the shown cage: the building's own box (Building), the lot box (Lot) or the lot box
        /// raised over on-lot upgrades (Both). The view offsets below refer to it.
        /// </summary>
        public CageBox Box { get; }

        public CageOffsets AppliedOffsets { get; }
        public CageOffsets TargetOffsets { get; }

        /// <summary>Both cages on the lot box: what the pipeline names and bakes, and what undo stores.</summary>
        public DeformSpec AppliedSpec { get; }

        public DeformSpec TargetSpec { get; }
        public DeformStatus Status { get; }

        /// <summary>The lot box both cages are stored on.</summary>
        public CageBox LotBox { get; }

        /// <summary>The other cage, drawn faintly while the cages differ (<see cref="HasGhost"/>).</summary>
        public CageBox GhostBox { get; }

        public CageOffsets GhostOffsets { get; }

        /// <summary>False for flat or meshless lots (parks): their building cage is the lot cage.</summary>
        public bool HasOwnBuildingBox { get; }

        /// <summary>The building is buried or floating above its lot's ground (only possible while split).</summary>
        public bool IsOffGround { get; }

        public bool IsSplit => !TargetSpec.IsLinked;
        public bool HasGhost => IsSplit && GhostOffsets != null;

        /// <summary>Whether the target is deformed at all.</summary>
        public bool IsDeformed => !TargetSpec.IsIdentity;

        /// <summary>Whether the target's lot cage is deformed; only then can its terrain be lifted.</summary>
        public bool LotDeformed => !TargetSpec.LotOffsets.IsIdentity;

        /// <summary>Whether the target's lot terrain follows the lot cage (the default for deformed lots).</summary>
        public bool TerrainLift => LotDeformed && !TargetSpec.FlatTerrain;

        /// <summary>The target's lot ground outline leaves the original lot (allowed, but may overlap neighbours).</summary>
        public bool ExceedsLot => LotBounds.ExceedsLot(LotBox, TargetSpec.LotOffsets);

        public float3 CornerPosition(int corner) => DeformedCage.CornerPosition(Box, TargetOffsets, corner);
        public float3 HandlePosition(CageHandle handle) => DeformedCage.HandlePosition(Box, TargetOffsets, handle);
    }
}
