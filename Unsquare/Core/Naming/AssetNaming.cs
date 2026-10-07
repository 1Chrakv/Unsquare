using System;

namespace Unsquare.Core.Naming
{
    /// <summary>
    /// Geometry asset name ("UQ_" + 16 hex) and GUID, both from one hash of render prefab name + deform key.
    /// The prefix lets housekeeping recognise our files; the seed prefix avoids clashes with other mods.
    /// </summary>
    public static class AssetNaming
    {
        public const string Prefix = "UQ_";
        private const int HashBytes = 8;
        private const string SeedPrefix = "Unsquare:";

        public static StableHash Hash(string renderName, string deformKey)
        {
            if (string.IsNullOrEmpty(renderName))
                throw new ArgumentException("Render name required.", nameof(renderName));
            if (string.IsNullOrEmpty(deformKey))
                throw new ArgumentException("Deform key required.", nameof(deformKey));
            return StableHash.Compute(SeedPrefix + renderName + ":" + deformKey);
        }

        public static string AssetName(StableHash hash) => Prefix + hash.ToHex(HashBytes);

        public static string AssetName(string renderName, string deformKey) => AssetName(Hash(renderName, deformKey));

        public static bool IsOwnAssetName(string name) =>
            name != null && name.StartsWith(Prefix, StringComparison.Ordinal) &&
            StableHash.IsLowerHex(name, Prefix.Length, HashBytes * 2);
    }
}
