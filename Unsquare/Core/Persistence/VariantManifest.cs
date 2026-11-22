using System;
using System.Collections.Generic;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// Legacy and frozen: variants that 1.0–1.1 saves reference by prefab ID, keyed by "BaseType/VariantName".
    /// Newer saves hold base + cage and need no entry. Also the format of the append-only archive.
    /// </summary>
    public sealed class VariantManifest
    {
        /// <summary>3 added the building cage (<see cref="ManifestEntry.BuildingOffsets"/>).</summary>
        public const int FormatVersion = 3;

        // Version 2 entries are linked variants: a missing building cage already reads as linked.
        private const int OldestMigratedVersion = 2;

        private readonly Dictionary<string, ManifestEntry> _entries = new(StringComparer.Ordinal);

        public int Count => _entries.Count;
        public IEnumerable<ManifestEntry> Entries => _entries.Values;
        public bool IsDirty { get; private set; }

        /// <summary>Entries dropped on load because the file had an older <see cref="FormatVersion"/>.</summary>
        public int DiscardedOutdatedCount { get; private set; }

        public bool TryGet(string key, out ManifestEntry entry) => _entries.TryGetValue(key, out entry);

        /// <summary>Adds or refreshes an entry; merges asset names and marks it used by the city.</summary>
        public void Register(ManifestEntry entry, string city, DateTime nowUtc)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            if (string.IsNullOrEmpty(entry.BaseType) || string.IsNullOrEmpty(entry.VariantName))
                throw new ArgumentException("BaseType and VariantName required.", nameof(entry));

            if (_entries.TryGetValue(entry.Key, out var existing))
            {
                foreach (var asset in entry.Assets)
                    AddUnique(existing.Assets, asset);
                foreach (var extension in entry.Extensions)
                    AddUnique(existing.Extensions, extension);
                existing.Offsets = entry.Offsets;
                existing.BuildingOffsets = entry.BuildingOffsets;
                existing.FlatTerrain = entry.FlatTerrain;
                existing.BlendMode = entry.BlendMode;
                entry = existing;
            }
            else
            {
                _entries.Add(entry.Key, entry);
            }
            entry.LastUsedUtc = nowUtc;
            if (!string.IsNullOrEmpty(city))
                AddUnique(entry.Cities, city);
            IsDirty = true;
        }

        public bool Remove(string key)
        {
            bool removed = _entries.Remove(key);
            IsDirty |= removed;
            return removed;
        }

        /// <summary>Adds asset names to an entry without touching its usage data (used when restoring).</summary>
        public bool MergeAssets(string key, IEnumerable<string> assets)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return false;
            bool changed = false;
            foreach (var asset in assets)
                changed |= AddUnique(entry.Assets, asset);
            IsDirty |= changed;
            return changed;
        }

        /// <summary>Lists an upgrade extension and its assets under an owner entry, keeping usage data.</summary>
        public bool AddExtension(string key, string extensionKey, IEnumerable<string> assets)
        {
            if (string.IsNullOrEmpty(extensionKey))
                throw new ArgumentException("Extension key required.", nameof(extensionKey));
            if (!_entries.TryGetValue(key, out var entry))
                return false;
            bool changed = AddUnique(entry.Extensions, extensionKey);
            foreach (var asset in assets)
                changed |= AddUnique(entry.Assets, asset);
            IsDirty |= changed;
            return changed;
        }

        /// <summary>Adds copies of the entries whose key is missing, usage data included; returns how many.</summary>
        /// <remarks>Append-only: existing entries are never changed, which is what the archive relies on.</remarks>
        public int AddMissing(IEnumerable<ManifestEntry> entries) => AddCopies(entries, null);

        /// <summary>
        /// Brings back copies of missing entries as freshly used by no city, so the next prune keeps them and the
        /// next load of a city marks them again. Returns how many.
        /// </summary>
        public int Revive(IEnumerable<ManifestEntry> entries, DateTime nowUtc) =>
            AddCopies(entries, copy =>
            {
                copy.Cities.Clear();
                copy.LastUsedUtc = nowUtc;
            });

        private int AddCopies(IEnumerable<ManifestEntry> entries, Action<ManifestEntry> adjust)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            int added = 0;
            foreach (var entry in entries)
            {
                if (entry?.BaseType == null || entry.VariantName == null || _entries.ContainsKey(entry.Key))
                    continue;
                var copy = entry.Copy();
                adjust?.Invoke(copy);
                _entries.Add(copy.Key, copy);
                added++;
            }
            IsDirty |= added > 0;
            return added;
        }

        /// <summary>Records which variants the loaded city uses: adds or removes the city per entry.</summary>
        public void MarkUsage(string city, ICollection<string> usedKeys, DateTime nowUtc)
        {
            if (string.IsNullOrEmpty(city))
                throw new ArgumentException("City name required.", nameof(city));
            foreach (var entry in _entries.Values)
            {
                if (usedKeys.Contains(entry.Key))
                {
                    AddUnique(entry.Cities, city);
                    entry.LastUsedUtc = nowUtc;
                }
                else
                {
                    entry.Cities.Remove(city);
                }
            }
            IsDirty = true;
        }

        public static JsonReadStatus Load(string path, out VariantManifest manifest)
        {
            manifest = new VariantManifest();
            var status = AtomicJsonFile.Read(path, out Document document);
            if (document?.Entries == null)
                return status;
            if (document.Version < OldestMigratedVersion || document.Version > FormatVersion)
            {
                // Formats from before the 1.0.0 release are not migrated: their variants are dropped.
                manifest.DiscardedOutdatedCount = document.Entries.Count;
                manifest.IsDirty = true;
                return status;
            }
            manifest.IsDirty = document.Version != FormatVersion;
            foreach (var entry in document.Entries)
            {
                if (entry?.BaseType == null || entry.VariantName == null)
                    continue;
                entry.Cities ??= new List<string>();
                entry.Assets ??= new List<string>();
                entry.Extensions ??= new List<string>();
                manifest._entries[entry.Key] = entry;
            }
            return status;
        }

        public void Save(string path)
        {
            AtomicJsonFile.Write(path, new Document { Version = FormatVersion, Entries = new List<ManifestEntry>(Entries) });
            IsDirty = false;
        }

        private static bool AddUnique(List<string> list, string value)
        {
            if (list.Contains(value))
                return false;
            list.Add(value);
            return true;
        }

        private sealed class Document
        {
            public int Version { get; set; }
            public List<ManifestEntry> Entries { get; set; }
        }
    }
}
