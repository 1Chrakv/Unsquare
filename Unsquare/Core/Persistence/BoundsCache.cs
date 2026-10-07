using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// Mesh bounds per geometry asset name. With the asset on disk and its bounds cached,
    /// restoring a variant needs no vertex work at all.
    /// </summary>
    public sealed class BoundsCache
    {
        private readonly Dictionary<string, MinMaxBounds> _bounds = new(StringComparer.Ordinal);

        public int Count => _bounds.Count;
        public bool IsDirty { get; private set; }

        public bool TryGet(string assetName, out MinMaxBounds bounds) => _bounds.TryGetValue(assetName, out bounds);

        public void Set(string assetName, MinMaxBounds bounds)
        {
            if (bounds.IsEmpty)
                throw new ArgumentException("Cannot cache empty bounds.", nameof(bounds));
            _bounds[assetName] = bounds;
            IsDirty = true;
        }

        public bool Remove(string assetName)
        {
            bool removed = _bounds.Remove(assetName);
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
                if (v == null || v.Length != 6)
                    continue;
                var bounds = new MinMaxBounds(new float3(v[0], v[1], v[2]), new float3(v[3], v[4], v[5]));
                if (!bounds.IsEmpty)
                    cache._bounds[pair.Key] = bounds;
            }
            return status;
        }

        public void Save(string path)
        {
            var raw = new Dictionary<string, float[]>(_bounds.Count, StringComparer.Ordinal);
            foreach (var pair in _bounds)
            {
                float3 min = pair.Value.Min, max = pair.Value.Max;
                raw[pair.Key] = new[] { min.x, min.y, min.z, max.x, max.y, max.z };
            }
            AtomicJsonFile.Write(path, raw);
            IsDirty = false;
        }
    }
}
