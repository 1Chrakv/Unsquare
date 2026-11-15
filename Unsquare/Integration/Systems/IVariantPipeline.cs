using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Options;
using Unsquare.Integration.Import;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// What the public API needs from the variant machinery. Offsets passed in are already validated;
    /// identity offsets revert the building to its base prefab (the terrain flag then does not matter).
    /// </summary>
    internal interface IVariantPipeline
    {
        /// <summary>Raised on the main thread whenever a building's status or applied shape changes.</summary>
        event Action<Entity> BuildingChanged;

        /// <summary>Tunables; new values apply to later requests, bakes and swaps.</summary>
        PipelineOptions Options { get; set; }

        bool TryDescribe(Entity building, out BuildingDescription description);

        /// <summary>Deformation of a request still in flight (debouncing or waiting to swap).</summary>
        bool TryGetRequested(Entity building, out DeformSpec spec);
        VariantStatus GetStatus(Entity building);
        void Request(Entity building, DeformSpec spec);

        /// <summary>Bakes the building's pending request in the next update instead of after the quiet delay.</summary>
        void BakeNow(Entity building);

        /// <summary>Swaps every deformed building back to its base prefab; returns the count.</summary>
        int ResetAll();

        /// <summary>
        /// Swaps a building on a foreign prefab (e.g. a FlexIt variant) to <paramref name="basePrefab"/> right away,
        /// cancelling requests in flight; false before the simulation is ready.
        /// </summary>
        bool TryRebase(Entity building, ObjectGeometryPrefab basePrefab);

        /// <summary>Rebases all FlexIt buildings and clears their shear; see <see cref="FlexItImporter"/>.</summary>
        FlexItImportBatch PrepareFlexItImport();

        /// <summary>
        /// On a Placemaker variant the player has not decided about yet: such buildings cannot be described (so not
        /// edited) until they are adopted.
        /// </summary>
        bool IsForeign(Entity building);

        /// <summary>
        /// Read-only description of an undecided Placemaker building for the hover preview: on its plain base with
        /// the cage Convert would give applied. Never used for edits.
        /// </summary>
        bool TryDescribeForeign(Entity building, out BuildingDescription description);

        /// <summary>
        /// Takes a Placemaker building over: it is described on Placemaker's plain base from now on, and requesting
        /// the returned spec swaps it straight to an Unsquare variant of the same shape.
        /// </summary>
        PlacemakerAdoption PreparePlacemakerAdoption(Entity building);

        /// <summary>Undoes <see cref="PreparePlacemakerAdoption"/> when the request was refused.</summary>
        void ReleaseAdoption(Entity building);

        /// <summary>
        /// The undecided Placemaker buildings in the building's row: itself and every house Placemaker joined to it,
        /// directly or along the row. Empty without Placemaker.
        /// </summary>
        IReadOnlyList<Entity> PlacemakerRow(Entity building);

        /// <summary>
        /// Holds the swaps of these buildings' requests until all are baked, so they land in the same frame. Members
        /// that fail or are cancelled stop being waited for; after a timeout the rest swap anyway.
        /// </summary>
        void SwapTogether(IReadOnlyCollection<Entity> buildings);
    }
}
