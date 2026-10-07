using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Core.Cage;

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

        bool TryGetCage(Entity building, out CageState state);

        /// <summary>
        /// Requests absolute offsets (debounced). For drags, keep the offsets from drag start and pass
        /// <c>start.Move(handle, totalDelta)</c> each frame to avoid rounding drift.
        /// </summary>
        DeformRequestResult SetOffsets(Entity building, CageOffsets offsets);

        /// <summary>Nudges a handle relative to the current target offsets, e.g. for +/- buttons.</summary>
        DeformRequestResult MoveHandle(Entity building, CageHandle handle, float3 delta);

        /// <summary>Reverts the building to its original prefab.</summary>
        DeformRequestResult Reset(Entity building);

        /// <summary>Reverts every deformed building in the city (uninstall path); returns how many.</summary>
        int ResetAll();
    }
}
