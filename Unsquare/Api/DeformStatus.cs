namespace Unsquare.Api
{
    public enum DeformStatus
    {
        /// <summary>The building uses its original prefab.</summary>
        Undeformed,

        /// <summary>The building shows <see cref="CageState.AppliedOffsets"/>; nothing in flight.</summary>
        Applied,

        /// <summary>A request waits for the debounce delay (further edits restart it).</summary>
        Pending,

        /// <summary>The variant is baked and swaps in once its prefab is initialized (usually next frame).</summary>
        Applying,

        /// <summary>The last request failed; the building keeps its previous shape. See the mod log.</summary>
        Failed,
    }
}
