using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Persistence
{
    /// <summary>One variant prefab that must be re-created on every preload while any city may use it.</summary>
    public sealed class ManifestEntry
    {
        public string BaseType { get; set; }
        public string BaseName { get; set; }
        public string VariantName { get; set; }

        /// <summary>Lot cage corner offsets in grid steps, see <see cref="Cage.CageOffsets.ToArray"/>.</summary>
        public int[] Offsets { get; set; }

        /// <summary>
        /// Building cage offsets (on the lot box) when they differ from <see cref="Offsets"/>; null means linked, as in
        /// every entry written before the split.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int[] BuildingOffsets { get; set; }

        /// <summary>Whether the variant keeps the flat lot terrain; missing in older files, meaning lifted.</summary>
        public bool FlatTerrain { get; set; }

        public DateTime LastUsedUtc { get; set; }

        /// <summary>Cities (by name) that used the variant when they were last loaded.</summary>
        public List<string> Cities { get; set; } = new();

        /// <summary>Geometry asset names (incl. LODs) baked for this variant; needed for safe deletion.</summary>
        public List<string> Assets { get; set; } = new();

        /// <summary>
        /// Upgrade extensions baked with this variant's cage, as "Type/BaseName" keys; their assets are in
        /// <see cref="Assets"/>. Missing in older files.
        /// </summary>
        public List<string> Extensions { get; set; } = new();

        [JsonIgnore]
        public string Key => MakeKey(BaseType, VariantName);

        /// <summary>Only the lot is deformed: the variant reuses the base meshes and owns no geometry assets.</summary>
        [JsonIgnore]
        public bool IsLotOnly => BuildingOffsets != null && IsZero(BuildingOffsets) && !IsZero(Offsets);

        public DeformSpec ToSpec()
        {
            var lot = CageOffsets.FromArray(Offsets);
            var building = BuildingOffsets == null ? lot : CageOffsets.FromArray(BuildingOffsets);
            return new DeformSpec(lot, building, FlatTerrain);
        }

        public void SetSpec(DeformSpec spec)
        {
            Offsets = spec.LotOffsets.ToArray();
            BuildingOffsets = spec.IsLinked ? null : spec.BuildingOffsets.ToArray();
            FlatTerrain = spec.FlatTerrain;
        }

        private static bool IsZero(int[] values)
        {
            if (values == null)
                return true;
            foreach (int value in values)
            {
                if (value != 0)
                    return false;
            }
            return true;
        }

        public static string MakeKey(string prefabType, string variantName) => prefabType + "/" + variantName;
    }
}
