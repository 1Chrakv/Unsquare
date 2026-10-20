using System.Collections.Generic;
using Unity.Entities;
using Unsquare.Core.Cage;

namespace Unsquare.Integration.Import
{
    /// <summary>
    /// Outcome of preparing a FlexIt import: buildings already moved back to their base prefab, plus the cage each
    /// convertible one should get. The caller requests those cages (with validation).
    /// </summary>
    internal sealed class FlexItImportBatch
    {
        public bool NoCity { get; set; }
        public bool FlexItMissing { get; set; }

        /// <summary>Curved FlexIt variants, now on their base prefab.</summary>
        public int ResetCount { get; set; }

        public int FailedCount { get; set; }

        public List<(Entity Building, DeformSpec Spec, string Source)> Conversions { get; } = new();
    }
}
