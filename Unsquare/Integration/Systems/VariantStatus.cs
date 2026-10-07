namespace Unsquare.Integration.Systems
{
    /// <summary>Where a building's latest request is in the bake → swap pipeline.</summary>
    internal enum VariantStatus
    {
        /// <summary>Nothing in flight; the building shows its applied offsets.</summary>
        Idle,

        /// <summary>Waiting for the debounce delay to pass.</summary>
        Pending,

        /// <summary>Waiting for the variant prefab entity to be initialized before swapping.</summary>
        WaitingForPrefab,

        /// <summary>The last request could not be baked or applied.</summary>
        Failed,
    }
}
