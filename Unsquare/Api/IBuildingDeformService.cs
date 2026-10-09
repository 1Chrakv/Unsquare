using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Options;

namespace Unsquare.Api
{
    /// <summary>
    /// UI entry point for deforming buildings via their cage. All positions and deltas are in building space
    /// (see <see cref="BuildingSpace"/>). Main thread only; obtain it via <see cref="UnsquareApi"/>.
    /// </summary>
    public interface IBuildingDeformService
    {
        /// <summary>Raised when a building's cage state changed; query it with <see cref="TryGetCage"/>.</summary>
        event Action<Entity> CageChanged;

        /// <summary>
        /// Cage of a building in the given scope, or in the scope currently applied/requested when null.
        /// A deformation in the other scope shows as undeformed here.
        /// </summary>
        bool TryGetCage(Entity building, CageScope? scope, out CageState state);

        /// <summary>
        /// Requests absolute offsets in a scope (debounced); replaces any deformation in the other scope. For drags,
        /// keep the offsets from drag start and pass <c>start.Move(handle, totalDelta)</c> to avoid rounding drift.
        /// </summary>
        DeformRequestResult SetOffsets(Entity building, CageScope scope, CageOffsets offsets);

        /// <summary>Nudges a handle relative to the current target offsets of a scope, e.g. for +/- buttons.</summary>
        DeformRequestResult MoveHandle(Entity building, CageScope scope, CageHandle handle, float3 delta);

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
