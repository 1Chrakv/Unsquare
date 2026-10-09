using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Naming
{
    /// <summary>
    /// Variant prefab names: "&lt;BaseName&gt;__uq&lt;12 hex&gt;", hashed from scope and offsets.
    /// Saves reference prefabs by this name, so the scheme (incl. both seeds) is permanent.
    /// </summary>
    public static class VariantNaming
    {
        public const string Separator = "__uq";
        private const int HashBytes = 6;

        // The building seed predates scopes; it must stay unchanged so existing saves keep their variants.
        private const string BuildingSeedPrefix = "Unsquare:offsets:v1:";
        private const string LotSeedPrefix = "Unsquare:lot-offsets:v1:";

        public static string VariantName(string baseName, CageScope scope, CageOffsets offsets)
        {
            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException("Base name required.", nameof(baseName));
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            string seed = (scope == CageScope.Lot ? LotSeedPrefix : BuildingSeedPrefix) + offsets.ToCanonicalString();
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
