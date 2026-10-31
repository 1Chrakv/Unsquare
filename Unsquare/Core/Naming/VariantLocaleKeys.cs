using System;
using System.Collections.Generic;

namespace Unsquare.Core.Naming
{
    /// <summary>
    /// Locale keys the game looks prefab names up by (title and description, for buildings and for upgrades). A variant
    /// gets the same texts as its base by aliasing its keys to the base's.
    /// </summary>
    public static class VariantLocaleKeys
    {
        private static readonly string[] Families =
        {
            "Assets.NAME", "Assets.DESCRIPTION", "Assets.UPGRADE_NAME", "Assets.UPGRADE_DESCRIPTION",
        };

        /// <summary>Pairs of (variant key, base key) for every key family.</summary>
        public static IReadOnlyList<(string VariantKey, string BaseKey)> Aliases(string variantName, string baseName)
        {
            if (string.IsNullOrEmpty(variantName))
                throw new ArgumentException("Variant name required.", nameof(variantName));
            if (string.IsNullOrEmpty(baseName))
                throw new ArgumentException("Base name required.", nameof(baseName));
            var aliases = new (string, string)[Families.Length];
            for (int i = 0; i < Families.Length; i++)
                aliases[i] = (Key(Families[i], variantName), Key(Families[i], baseName));
            return aliases;
        }

        private static string Key(string family, string prefabName) => family + "[" + prefabName + "]";
    }
}
