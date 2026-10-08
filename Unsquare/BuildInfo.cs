using System.Reflection;

namespace Unsquare
{
    /// <summary>
    /// Version and build stamp of the loaded mod assembly, logged on load so testers can see which build runs.
    /// The version comes from &lt;Version&gt; in Unsquare.csproj.
    /// </summary>
    internal static class BuildInfo
    {
#if DEBUG
        public const string Configuration = "Debug";
#else
        public const string Configuration = "Release";
#endif

        private static readonly Assembly ModAssembly = typeof(BuildInfo).Assembly;

        public static readonly string Version = ReadVersion();
        public static readonly string BuildTimeUtc = ReadMetadata("BuildTimeUtc") ?? "unknown";

        /// <summary>E.g. "0.1.0+abc1234 (Debug, built 2026-10-08 19:55:00 UTC)".</summary>
        public static string Summary => $"{Version} ({Configuration}, built {BuildTimeUtc} UTC)";

        // The SDK appends "+<full git hash>" of HEAD at build time; 7 characters identify the commit.
        private static string ReadVersion()
        {
            var informational = ModAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (informational == null)
                return ModAssembly.GetName().Version.ToString();
            string version = informational.InformationalVersion;
            int plus = version.IndexOf('+');
            return plus >= 0 && version.Length > plus + 8 ? version.Substring(0, plus + 8) : version;
        }

        private static string ReadMetadata(string key)
        {
            foreach (var attribute in ModAssembly.GetCustomAttributes<AssemblyMetadataAttribute>())
            {
                if (attribute.Key == key)
                    return attribute.Value;
            }
            return null;
        }
    }
}
