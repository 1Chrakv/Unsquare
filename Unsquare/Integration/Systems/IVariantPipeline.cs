using System;
using Unity.Entities;
using Unsquare.Core.Cage;
using Unsquare.Core.Options;

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

        /// <summary>Swaps every deformed building back to its base prefab; returns the count.</summary>
        int ResetAll();
    }
}
