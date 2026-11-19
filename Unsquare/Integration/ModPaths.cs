using System.IO;
using Colossal.PSI.Environment;

namespace Unsquare.Integration
{
    /// <summary>Where Unsquare keeps its manifest, bounds cache and baked geometry.</summary>
    internal static class ModPaths
    {
        /// <summary>Asset database sub path for baked geometry (relative to the user database).</summary>
        public const string AssetSubPath = "ModsData/Unsquare";

        public static string DataDirectory => Path.Combine(EnvPath.kUserDataPath, "ModsData", "Unsquare");
        public static string ManifestFile => Path.Combine(DataDirectory, "variants.json");
        public static string BoundsCacheFile => Path.Combine(DataDirectory, "bounds.json");

        /// <summary>Append-only copy of every legacy manifest entry ever seen; saves depend on it like the manifest.</summary>
        public static string LegacyArchiveFile => Path.Combine(DataDirectory, "variants.archive.json");
    }
}
