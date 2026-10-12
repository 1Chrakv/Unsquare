using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Options;
using Unsquare.Core.Terrain;

namespace Unsquare.Api
{
    /// <summary>
    /// UI entry point for deforming buildings via their lot cage. All positions and deltas are in building space
    /// (see <see cref="BuildingSpace"/>). Main thread only; obtain it via <see cref="UnsquareApi"/>.
    /// </summary>
    public interface IBuildingDeformService
    {
        /// <summary>Raised when a building's cage state changed; query it with <see cref="TryGetCage"/>.</summary>
        event Action<Entity> CageChanged;

        /// <summary>Lot cage of a building; false for objects without a lot (not deformable).</summary>
        bool TryGetCage(Entity building, out CageState state);

        /// <summary>
        /// Requests absolute offsets (debounced), keeping the terrain flag. For drags, keep the offsets from drag
        /// start and pass <c>start.Move(handle, totalDelta)</c> to avoid rounding drift.
        /// </summary>
        DeformRequestResult SetOffsets(Entity building, CageOffsets offsets);

        /// <summary>Switches whether the lot terrain follows the cage; rejected while the building is undeformed.</summary>
        DeformRequestResult SetTerrainLift(Entity building, bool lift);

        /// <summary>
        /// Moves the bottom corners (with the top corners above them) onto the original terrain, before lots and roads
        /// flattened it; see <see cref="GroundFitMode"/>. Asynchronous: <paramref name="done"/> gets the deformation
        /// before the fit and the result.
        /// </summary>
        /// <returns>False if the building is not deformable or the terrain cannot be read.</returns>
        bool TryFitToGround(Entity building, GroundFitMode mode, Action<DeformSpec, DeformRequestResult> done);

        /// <summary>Requests offsets and terrain flag together, e.g. to replay an undo step.</summary>
        DeformRequestResult Apply(Entity building, DeformSpec spec);

        /// <summary>Nudges a handle relative to the current target offsets, e.g. for +/- buttons.</summary>
        DeformRequestResult MoveHandle(Entity building, CageHandle handle, float3 delta);

        /// <summary>Reverts the building to its original prefab.</summary>
        DeformRequestResult Reset(Entity building);

        /// <summary>Reverts every deformed building in the city (uninstall path); returns how many.</summary>
        int ResetAll();

        /// <summary>Current tunables (limits, attachments, rebake delay, pruning, lot sync).</summary>
        PipelineOptions Options { get; }

        /// <summary>Replaces the tunables; applies to later requests and new variants, not to existing ones.</summary>
        void Configure(PipelineOptions options);
    }
}
