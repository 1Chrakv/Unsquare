using System;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// Deformed buildings of a loaded city tied to saves from before 1.2: on restored variants (saving converts them),
    /// on missing variants only in the archive (restoring brings them back), or, for a converted save, the legacy
    /// variants still restored at every start for other old saves.
    /// </summary>
    public readonly struct LegacySaveCounts
    {
        public LegacySaveCounts(int convertible, int archived, int remaining)
        {
            if (convertible < 0 || archived < 0 || remaining < 0)
                throw new ArgumentException("Counts must not be negative.");
            Convertible = convertible;
            Archived = archived;
            Remaining = remaining;
        }

        public int Convertible { get; }

        public int Archived { get; }

        /// <summary>Legacy manifest entries; only set when a save in the new format was loaded.</summary>
        public int Remaining { get; }

        public bool Any => Convertible > 0 || Archived > 0 || Remaining > 0;
    }
}
