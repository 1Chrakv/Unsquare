using System;
using System.Collections.Generic;
using System.IO;
using Unsquare.Core.Persistence;

namespace Unsquare.Integration.Saves
{
    /// <summary>
    /// The frozen manifest of variants that saves from before 1.2 need, and its append-only archive: loading,
    /// rebuilding a lost manifest from the archive, retiring the manifest and bringing archived entries back.
    /// </summary>
    internal sealed class LegacyManifestStore
    {
        private readonly List<ManifestEntry> _failures = new();
        private VariantManifest _archive = new();
        private Dictionary<string, string> _archiveByName; // variant name → key; rebuilt when the archive changes
        private bool _archiveWritable;

        public VariantManifest Manifest { get; private set; } = new();

        /// <summary>Loads manifest and archive; false if the manifest is untrusted for pruning this session.</summary>
        public bool Load()
        {
            var manifestStatus = VariantManifest.Load(ModPaths.ManifestFile, out var manifest);
            Manifest = manifest;
            if (Manifest.DiscardedOutdatedCount > 0)
                Mod.Log.Warn($"Variant manifest has an old format: discarded {Manifest.DiscardedOutdatedCount} variants. " +
                             "Deformed buildings in older saves revert or break; delete ModsData/Unsquare to free space.");

            var archiveStatus = VariantManifest.Load(ModPaths.LegacyArchiveFile, out _archive);
            _archiveByName = null;
            // A corrupt archive is never overwritten: it may hold the only copy of pruned entries.
            _archiveWritable = archiveStatus != JsonReadStatus.Corrupt;
            if (!_archiveWritable)
                Mod.Log.Error("Legacy manifest archive (variants.archive.json) is unreadable; it is left as it is.");

            switch (manifestStatus)
            {
                case JsonReadStatus.Corrupt:
                    Mod.Log.Error("Variant manifest is unreadable; deformed buildings in saves may break.");
                    PreserveCorruptManifest();
                    ReviveFromArchive();
                    return false;
                case JsonReadStatus.FromBackup:
                    Mod.Log.Warn("Variant manifest restored from backup.");
                    return false;
                case JsonReadStatus.Missing when _archive.Count > 0:
                    ReviveFromArchive();
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Appends entries the archive lacks; run before the prune, so pruned entries stay recoverable.</summary>
        public void ArchiveEntries()
        {
            try
            {
                if (_archiveWritable && _archive.AddMissing(Manifest.Entries) > 0)
                {
                    _archiveByName = null;
                    _archive.Save(ModPaths.LegacyArchiveFile);
                    Mod.Log.Info($"Legacy manifest archive holds {_archive.Count} variants.");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Writing the legacy manifest archive failed.");
            }
        }

        /// <summary>Entries whose restore failed at the last preload; they block retiring for their cities.</summary>
        public void ClearFailures() => _failures.Clear();

        public void RecordFailure(ManifestEntry entry) => _failures.Add(entry);

        /// <param name="loadedCity">The city in play, or null.</param>
        /// <param name="failedBuildings">Sorted base names of this city's entries that failed to load; the player must
        /// deform them again, since resaving the city loses their old shapes.</param>
        public bool TryRetire(string loadedCity, out int retired, out List<string> failedBuildings, out string refusal)
        {
            retired = 0;
            failedBuildings = FailedBuildingsOf(loadedCity);
            if (!_archiveWritable)
            {
                refusal = "the archive file is unreadable, see the log";
                return false;
            }
            if (failedBuildings.Count > 0)
                Mod.Log.Warn($"Retiring although these buildings of {loadedCity} failed to load their old shape: " +
                             string.Join(", ", failedBuildings));
            try
            {
                _archive.AddMissing(Manifest.Entries);
                _archiveByName = null;
                _archive.Save(ModPaths.LegacyArchiveFile);
                retired = Manifest.Count;
                // An empty file, not a deleted one: a missing manifest would be rebuilt from the archive.
                Manifest = new VariantManifest();
                Manifest.Save(ModPaths.ManifestFile);
                Mod.Log.Info($"Legacy manifest retired: {retired} variants moved to the archive.");
                refusal = null;
                return true;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Retiring the legacy manifest failed.");
                refusal = "writing the files failed, see the log";
                return false;
            }
        }

        private List<string> FailedBuildingsOf(string city)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(city))
            {
                foreach (var entry in _failures)
                {
                    if (entry.Cities.Contains(city))
                        names.Add(entry.BaseName);
                }
            }
            return new List<string>(names);
        }

        /// <summary>
        /// Key of the archived entry of a variant the manifest lacks; false when the manifest has it (then its restore
        /// failed, which the archive can't fix) or the archive doesn't.
        /// </summary>
        public bool TryFindArchivedOnly(string variantName, out string key)
        {
            if (_archiveByName == null)
            {
                _archiveByName = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in _archive.Entries)
                    _archiveByName[entry.VariantName] = entry.Key;
            }
            return _archiveByName.TryGetValue(variantName, out key) && !Manifest.TryGet(key, out _);
        }

        /// <summary>
        /// Revives archived entries in the manifest: those of <paramref name="keys"/>, or all when null. The caller
        /// saves it.
        /// </summary>
        public bool TryRestore(ICollection<string> keys, out int restored, out string refusal)
        {
            restored = 0;
            if (!_archiveWritable)
            {
                refusal = "the archive file is unreadable, see the log";
                return false;
            }
            var entries = keys == null ? _archive.Entries : SelectArchived(keys);
            restored = Manifest.Revive(entries, DateTime.UtcNow);
            Mod.Log.Info($"Restored {restored} archived variants of old saves to the manifest; they load with the next save.");
            refusal = null;
            return true;
        }

        private List<ManifestEntry> SelectArchived(ICollection<string> keys)
        {
            var selected = new List<ManifestEntry>();
            foreach (var entry in _archive.Entries)
            {
                if (keys.Contains(entry.Key))
                    selected.Add(entry);
            }
            return selected;
        }

        /// <summary>Writes the manifest if it changed; throws on I/O errors.</summary>
        public void SaveIfDirty()
        {
            if (Manifest.IsDirty)
                Manifest.Save(ModPaths.ManifestFile);
        }

        // Lost manifest: old saves still find their variants. Untrusted for this session, so nothing is pruned.
        private void ReviveFromArchive()
        {
            int revived = Manifest.Revive(_archive.Entries, DateTime.UtcNow);
            if (revived > 0)
                Mod.Log.Warn($"Variant manifest rebuilt from the legacy archive: {revived} variants of old saves.");
        }

        private static void PreserveCorruptManifest()
        {
            try
            {
                string target = ModPaths.ManifestFile + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                if (File.Exists(ModPaths.ManifestFile))
                    File.Copy(ModPaths.ManifestFile, target);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Could not preserve corrupt manifest.");
            }
        }
    }
}
