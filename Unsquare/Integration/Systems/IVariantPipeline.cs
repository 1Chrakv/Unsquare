using System;
using System.Collections.Generic;
using Game.Prefabs;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Options;
using Unsquare.Core.Persistence;
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
        /// Moves the legacy manifest into the archive and leaves it empty: from the next start, saves from before
        /// saved cages are no longer restored. Names the loaded city's buildings whose old shape failed to load.
        /// </summary>
        bool TryRetireLegacyManifest(out int retired, out IReadOnlyList<string> failedBuildings, out string refusal);

        /// <summary>Brings archived variants of old saves back into the manifest; they load from the next save loaded.</summary>
        bool TryRestoreLegacyShapes(out int restored, out string refusal);

        /// <summary>
        /// Revives only the archived variants the loaded city is missing (see <see cref="LegacySaveCounts.Archived"/>);
        /// they load when the save is loaded again. Refused when there are none.
        /// </summary>
        bool TryRestoreArchivedShapes(out int restored, out string refusal);

        /// <summary>A city was loaded with deformed buildings in the old save format (only raised when there are any).</summary>
        event Action<LegacySaveCounts> LegacySaveLoaded;

        /// <summary>
        /// Read-only description of a building on another mod's variant that was not taken over yet (see
        /// <see cref="IForeignPrefabs"/>), for the hover preview: on its plain base with the takeover's cage applied.
        /// </summary>
        bool TryDescribeForeign(Entity building, out BuildingDescription description);

        /// <summary>
        /// Holds the swaps of these buildings' requests until all are baked, so they land in the same frame. Members
        /// that fail or are cancelled stop being waited for; after a timeout the rest swap anyway.
        /// </summary>
        void SwapTogether(IReadOnlyCollection<Entity> buildings);
    }
}
