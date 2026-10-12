using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Naming
{
    /// <summary>
    /// Variant prefab names: "&lt;BaseName&gt;__uq&lt;12 hex&gt;", hashed from the lot cage offsets (and the flat-terrain
    /// flag when set). Saves reference prefabs by this name, so changing the scheme or seed breaks saves.
    /// </summary>
    public static class VariantNaming
    {
        public const string Separator = "__uq";
        private const int HashBytes = 6;

        private const string SeedPrefix = "Unsquare:lot-offsets:v1:";
        private const string FlatTerrainSuffix = "|flat-terrain";

        // Lifted terrain keeps the original seed, so names from before the flag existed stay valid.
        public static string VariantName(string baseName, CageOffsets offsets, bool flatTerrain = false)
        {
            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException("Base name required.", nameof(baseName));
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            string seed = SeedPrefix + offsets.ToCanonicalString() + (flatTerrain ? FlatTerrainSuffix : "");
            return baseName + Separator + StableHash.Compute(seed).ToHex(HashBytes);
        }

        /// <summary>Extracts the base prefab name if <paramref name="name"/> is a variant name.</summary>
        public static bool TryParse(string name, out string baseName)
        {
            baseName = null;
            if (string.IsNullOrEmpty(name))
                return false;
            int at = name.LastIndexOf(Separator, StringComparison.Ordinal);
            if (at <= 0 || !StableHash.IsLowerHex(name, at + Separator.Length, HashBytes * 2))
                return false;
            baseName = name.Substring(0, at);
            return true;
        }

        public static bool IsVariantName(string name) => TryParse(name, out _);
    }
}
