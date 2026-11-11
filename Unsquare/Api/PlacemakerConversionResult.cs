namespace Unsquare.Api
{
    /// <summary>Outcome of <see cref="IBuildingDeformService.ConvertPlacemaker"/>.</summary>
    public enum PlacemakerConversionResult
    {
        /// <summary>Unsquare's cage reproduces Placemaker's shape; the variant swaps in after its bake.</summary>
        Converted,

        /// <summary>Converted with the closest cage: Placemaker's yard shift can't be matched exactly (see the log).</summary>
        Approximated,

        /// <summary>Converted, but the shape went past the cage limits and was clamped.</summary>
        Clamped,

        /// <summary>Placemaker is reshaping the building right now; try again a moment later.</summary>
        PlacemakerBusy,

        /// <summary>Placemaker is not running or has changed; the building stays as it is.</summary>
        PlacemakerMissing,

        /// <summary>No base prefab, no lot or a cage that folds; the building stays as it is (see the log).</summary>
        Failed,
    }
}
