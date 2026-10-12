using System;
using System.Collections.Generic;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// All variants ever registered, keyed by "BaseType/VariantName". Losing it breaks every save
    /// that uses variants, so it is only written through <see cref="AtomicJsonFile"/>.
    /// </summary>
    public sealed class VariantManifest
    {
        public const int FormatVersion = 2;

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
                existing.Offsets = entry.Offsets;
                existing.FlatTerrain = entry.FlatTerrain;
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
            if (document.Version != FormatVersion)
            {
                // Older formats are not migrated (early development): their variants are dropped.
                manifest.DiscardedOutdatedCount = document.Entries.Count;
                manifest.IsDirty = true;
                return status;
            }
            foreach (var entry in document.Entries)
            {
                if (entry?.BaseType == null || entry.VariantName == null)
                    continue;
                entry.Cities ??= new List<string>();
                entry.Assets ??= new List<string>();
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
