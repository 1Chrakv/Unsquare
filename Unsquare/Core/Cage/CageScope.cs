namespace Unsquare.Core.Cage
{
    /// <summary>
    /// What the cage spans. Stored by name in the manifest and part of variant naming, so values are permanent.
    /// </summary>
    public enum CageScope
    {
        /// <summary>The main building's mesh bounds; attachments outside it stay untouched.</summary>
        Building,

        /// <summary>The whole lot rectangle from ground to roof; everything on the lot deforms.</summary>
        Lot,
    }
}
