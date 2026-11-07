using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.Core.Options;
using Unsquare.Core.Terrain;

namespace Unsquare.Api
{
    /// <summary>
    /// UI entry point for deforming buildings via their two cages (building and lot, see <see cref="CageTarget"/>).
    /// All positions and deltas are in building space (see <see cref="BuildingSpace"/>). Main thread only; obtain it
    /// via <see cref="UnsquareApi"/>.
    /// </summary>
    public interface IBuildingDeformService
    {
        /// <summary>Raised when a building's cage state changed; query it with <see cref="TryGetCage"/>.</summary>
        event Action<Entity> CageChanged;

        /// <summary>A building's cage as the target sees it; false for objects without a lot (not deformable).</summary>
        bool TryGetCage(Entity building, CageTarget target, out CageState state);

        /// <summary>
        /// Moves the start state's cage to <paramref name="view"/> (offsets on <see cref="CageState.Box"/>), debounced.
        /// Drags pass the state from drag start, so rounding never drifts; other edits pass the current state.
        /// </summary>
        DeformRequestResult SetOffsets(Entity building, in CageState start, CageOffsets view);

        /// <summary>
        /// Sets what follows the building's walls on the ground (<see cref="BlendMode"/>). While the cages are linked it
        /// is kept for the next split (in memory only, since linked variants don't depend on it).
        /// </summary>
        DeformRequestResult SetBlendMode(Entity building, BlendMode mode);

        /// <summary>Switches whether the lot terrain follows the lot cage; rejected while the lot is undeformed.</summary>
        DeformRequestResult SetTerrainLift(Entity building, bool lift);

        /// <summary>
        /// Lot and Both: moves the bottom corners onto the original terrain, before lots and roads flattened it (see
        /// <see cref="GroundFitMode"/>). Building: seats the building on its lot's ground. <paramref name="done"/> gets
        /// the deformation before the fit and the result, possibly before this returns.
        /// </summary>
        /// <returns>False if the building is not deformable or the terrain cannot be read.</returns>
        bool TryFitToGround(Entity building, CageTarget target, GroundFitMode mode,
            Action<DeformSpec, DeformRequestResult> done);

        /// <summary>
        /// Aligns the target cage's left, back or right face to the nearest road or neighbour edge (see
        /// <see cref="AlignMode"/>): their building cages in Building mode, their lots otherwise. Rejected when there
        /// is no neighbour within the options' reach.
        /// </summary>
        DeformRequestResult AlignToNeighbour(Entity building, CageTarget target, AlignSide side, AlignMode mode,
            NeighbourAlignOptions options);

        /// <summary>
        /// Moves the bottom of the target cage's left, back or right side up or down to the ground height of the
        /// neighbour that <see cref="AlignToNeighbour"/> would use; rejected when there is none.
        /// </summary>
        DeformRequestResult AlignGroundToNeighbour(Entity building, CageTarget target, AlignSide side,
            NeighbourAlignOptions options);

        /// <summary>Requests both cages and the terrain flag as given on the lot box, e.g. to replay an undo step.</summary>
        DeformRequestResult Apply(Entity building, DeformSpec spec);

        /// <summary>Nudges a handle of the target cage relative to its current view, e.g. for +/- buttons.</summary>
        DeformRequestResult MoveHandle(Entity building, CageTarget target, CageHandle handle, float3 delta);

        /// <summary>Reverts the target cage to its original shape; Both reverts the building to its original prefab.</summary>
        DeformRequestResult Reset(Entity building, CageTarget target);

        /// <summary>
        /// Links the cages again: <paramref name="follower"/> Building drops the building's own shape, Lot gives the
        /// lot the building's shape. Rejected while linked.
        /// </summary>
        DeformRequestResult Link(Entity building, CageTarget follower);

        /// <summary>Reverts every deformed building in the city (uninstall path); returns how many.</summary>
        int ResetAll();

        /// <summary>
        /// Converts the city's FlexIt buildings (FlexIt must be running): tapers and shears become Unsquare cages,
        /// corner fits become the closest cage, arches and bends go back to their original prefab. Same entity.
        /// </summary>
        FlexItImportResult ImportFlexIt();

        /// <summary>Current tunables (limits, attachments, rebake delay, pruning, lot sync).</summary>
        PipelineOptions Options { get; }

        /// <summary>Replaces the tunables; applies to later requests and new variants, not to existing ones.</summary>
        void Configure(PipelineOptions options);
    }
}
