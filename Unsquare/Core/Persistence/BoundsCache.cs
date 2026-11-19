using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// Mesh bounds and last use per geometry asset name. With the asset on disk and its bounds cached, restoring a
    /// variant needs no vertex work; the last use orders the geometry cache's eviction.
    /// </summary>
    /// <remarks>A cache, not save-relevant: entries are [min xyz, max xyz] plus an optional last use in Unix days.</remarks>
    public sealed class BoundsCache
    {
        /// <summary>Touches closer than this to the stored last use don't rewrite the file.</summary>
        public static readonly TimeSpan TouchResolution = TimeSpan.FromHours(1);

        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

        public int Count => _entries.Count;
        public bool IsDirty { get; private set; }

        /// <summary>Asset names with an entry, as a copy that may be iterated while removing.</summary>
        public List<string> Names => new(_entries.Keys);

        public bool TryGet(string assetName, out MinMaxBounds bounds)
        {
            bool found = _entries.TryGetValue(assetName, out var entry);
            bounds = entry.Bounds;
            return found;
        }

        public bool TryGetLastUsed(string assetName, out DateTime lastUsedUtc)
        {
            lastUsedUtc = default;
            if (!_entries.TryGetValue(assetName, out var entry) || float.IsNaN(entry.LastUsedDays))
                return false;
            lastUsedUtc = UnixEpoch.AddDays(entry.LastUsedDays);
            return true;
        }

        /// <summary>Stores bounds; an existing last use is kept.</summary>
        public void Set(string assetName, MinMaxBounds bounds)
        {
            if (bounds.IsEmpty)
                throw new ArgumentException("Cannot cache empty bounds.", nameof(bounds));
            float lastUsed = _entries.TryGetValue(assetName, out var old) ? old.LastUsedDays : float.NaN;
            _entries[assetName] = new Entry(bounds, lastUsed);
            IsDirty = true;
        }

        /// <summary>Records a use of a known asset; dirty only once the stored use is over an hour old.</summary>
        public void Touch(string assetName, DateTime nowUtc)
        {
            if (!_entries.TryGetValue(assetName, out var entry))
                return;
            float now = ToDays(nowUtc);
            if (!float.IsNaN(entry.LastUsedDays) && now - entry.LastUsedDays < TouchResolution.TotalDays)
                return;
            _entries[assetName] = new Entry(entry.Bounds, now);
            IsDirty = true;
        }

        public bool Remove(string assetName)
        {
            bool removed = _entries.Remove(assetName);
            IsDirty |= removed;
            return removed;
        }

        public static JsonReadStatus Load(string path, out BoundsCache cache)
        {
            cache = new BoundsCache();
            var status = AtomicJsonFile.Read(path, out Dictionary<string, float[]> raw);
            if (raw == null)
                return status;
            foreach (var pair in raw)
            {
                float[] v = pair.Value;
                if (v == null || (v.Length != 6 && v.Length != 7))
                    continue;
                var bounds = new MinMaxBounds(new float3(v[0], v[1], v[2]), new float3(v[3], v[4], v[5]));
                if (!bounds.IsEmpty)
                    cache._entries[pair.Key] = new Entry(bounds, v.Length == 7 ? v[6] : float.NaN);
            }
            return status;
        }

        public void Save(string path)
        {
            var raw = new Dictionary<string, float[]>(_entries.Count, StringComparer.Ordinal);
            foreach (var pair in _entries)
            {
                float3 min = pair.Value.Bounds.Min, max = pair.Value.Bounds.Max;
                float lastUsed = pair.Value.LastUsedDays;
                raw[pair.Key] = float.IsNaN(lastUsed)
                    ? new[] { min.x, min.y, min.z, max.x, max.y, max.z }
                    : new[] { min.x, min.y, min.z, max.x, max.y, max.z, lastUsed };
            }
            AtomicJsonFile.Write(path, raw);
            IsDirty = false;
        }

        private static float ToDays(DateTime utc) => (float)(utc.ToUniversalTime() - UnixEpoch).TotalDays;

        private readonly struct Entry
        {
            public Entry(MinMaxBounds bounds, float lastUsedDays)
            {
                Bounds = bounds;
                LastUsedDays = lastUsedDays;
            }

            public MinMaxBounds Bounds { get; }

            /// <summary>Unix days (about 3 min resolution as float); NaN when never touched.</summary>
            public float LastUsedDays { get; }
        }
    }
}
