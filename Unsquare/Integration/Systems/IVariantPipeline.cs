using System;
using Unity.Entities;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Systems
{
    /// <summary>
    /// What the public API needs from the variant machinery. Shapes passed in are already validated;
    /// identity offsets revert the building to its base prefab.
    /// </summary>
    internal interface IVariantPipeline
    {
        /// <summary>Raised on the main thread whenever a building's status or applied shape changes.</summary>
        event Action<Entity> BuildingChanged;

        bool TryDescribe(Entity building, out BuildingDescription description);

        /// <summary>Shape of a request still in flight (debouncing or waiting to swap).</summary>
        bool TryGetRequested(Entity building, out CageShape shape);
        VariantStatus GetStatus(Entity building);
        void Request(Entity building, CageShape shape);

        /// <summary>Swaps every deformed building back to its base prefab; returns the count.</summary>
        int ResetAll();
    }
}
