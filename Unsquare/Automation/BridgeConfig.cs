#if UNSQUARE_AUTOMATION
using System;
using System.IO;
using Newtonsoft.Json.Linq;
using Unsquare.Integration;

namespace Unsquare.Automation
{
    /// <summary>
    /// Whether the automation bridge runs and on which port. Off unless <c>automation\enabled</c> exists in the mod's
    /// data folder; each start writes a fresh token to <c>automation\token</c> that clients must send.
    /// </summary>
    internal sealed class BridgeConfig
    {
        public const int DefaultPort = 7337;

        private BridgeConfig(bool enabled, int port, string token)
        {
            Enabled = enabled;
            Port = port;
            Token = token;
        }

        public bool Enabled { get; }
        public int Port { get; }
        public string Token { get; }

        public static string Directory => Path.Combine(ModPaths.DataDirectory, "automation");
        private static string EnabledFile => Path.Combine(Directory, "enabled");
        private static string ConfigFile => Path.Combine(Directory, "config.json");
        private static string TokenFile => Path.Combine(Directory, "token");

        public static BridgeConfig Load()
        {
            if (!File.Exists(EnabledFile))
                return new BridgeConfig(false, 0, null);
            var port = DefaultPort;
            try
            {
                if (File.Exists(ConfigFile))
                    port = JObject.Parse(File.ReadAllText(ConfigFile)).Value<int?>("port") ?? DefaultPort;
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"[Auto] Ignoring {ConfigFile}: {e.Message}");
            }
            var token = Guid.NewGuid().ToString("N");
            File.WriteAllText(TokenFile, token);
            return new BridgeConfig(true, port, token);
        }
    }
}
#endif
