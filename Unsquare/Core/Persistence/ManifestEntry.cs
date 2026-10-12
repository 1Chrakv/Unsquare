using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Unsquare.Core.Persistence
{
    /// <summary>One variant prefab that must be re-created on every preload while any city may use it.</summary>
    public sealed class ManifestEntry
    {
        public string BaseType { get; set; }
        public string BaseName { get; set; }
        public string VariantName { get; set; }

        /// <summary>Corner offsets in grid steps, see <see cref="Cage.CageOffsets.ToArray"/>.</summary>
        public int[] Offsets { get; set; }

        /// <summary>Whether the variant keeps the flat lot terrain; missing in older files, meaning lifted.</summary>
        public bool FlatTerrain { get; set; }

        public DateTime LastUsedUtc { get; set; }

        /// <summary>Cities (by name) that used the variant when they were last loaded.</summary>
        public List<string> Cities { get; set; } = new();

        /// <summary>Geometry asset names (incl. LODs) baked for this variant; needed for safe deletion.</summary>
        public List<string> Assets { get; set; } = new();

        [JsonIgnore]
        public string Key => MakeKey(BaseType, VariantName);

        public static string MakeKey(string prefabType, string variantName) => prefabType + "/" + variantName;
    }
}
