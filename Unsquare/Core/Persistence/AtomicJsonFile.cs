using System;
using System.IO;
using Newtonsoft.Json;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// Crash-safe JSON files: write to "*.tmp", then swap in and keep the previous version as "*.bak".
    /// Reading falls back to the backup if the main file is missing or unreadable.
    /// </summary>
    public static class AtomicJsonFile
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.None,
        };

        public static string BackupPath(string path) => path + ".bak";

        public static void Write<T>(string path, T value)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path required.", nameof(path));
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(value, Settings));
            if (File.Exists(path))
                File.Replace(tmp, path, BackupPath(path));
            else
                File.Move(tmp, path);
        }

        public static JsonReadStatus Read<T>(string path, out T value) where T : class
        {
            string backup = BackupPath(path);
            bool mainExists = File.Exists(path), backupExists = File.Exists(backup);
            if (mainExists && TryParse(path, out value))
                return JsonReadStatus.Ok;
            if (backupExists && TryParse(backup, out value))
                return JsonReadStatus.FromBackup;
            value = null;
            return mainExists || backupExists ? JsonReadStatus.Corrupt : JsonReadStatus.Missing;
        }

        private static bool TryParse<T>(string file, out T value) where T : class
        {
            try
            {
                value = JsonConvert.DeserializeObject<T>(File.ReadAllText(file), Settings);
                return value != null;
            }
            catch (Exception e) when (e is JsonException || e is IOException || e is UnauthorizedAccessException)
            {
                value = null;
                return false;
            }
        }
    }
}
