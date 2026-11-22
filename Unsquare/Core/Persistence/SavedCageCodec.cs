using System;
using System.Collections.Generic;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Persistence
{
    /// <summary>
    /// The deformation a save stores on a building, as a versioned list of ints. Part of the save format: a change
    /// bumps <see cref="FormatVersion"/>, and older formats must keep decoding.
    /// </summary>
    public static class SavedCageCodec
    {
        /// <summary>Format of <see cref="Encode"/>; 0 never decodes (marks unreadable data).</summary>
        public const int FormatVersion = 1;

        /// <summary>Most values any format writes: flags, blend mode and two cages.</summary>
        public const int MaxValueCount = 2 + 2 * CageOffsets.ValueCount;

        private const int LinkedFlag = 1;
        private const int FlatTerrainFlag = 2;
        private const int KnownFlags = LinkedFlag | FlatTerrainFlag;

        /// <summary>v1: flags, blend mode, the lot cage and, unless linked, the building cage.</summary>
        public static void Encode(DeformSpec spec, List<int> values)
        {
            if (spec == null)
                throw new ArgumentNullException(nameof(spec));
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            values.Clear();
            int flags = (spec.IsLinked ? LinkedFlag : 0) | (spec.FlatTerrain ? FlatTerrainFlag : 0);
            values.Add(flags);
            values.Add((int)spec.BlendMode);
            values.AddRange(spec.LotOffsets.ToArray());
            if (!spec.IsLinked)
                values.AddRange(spec.BuildingOffsets.ToArray());
        }

        /// <summary>The spec as its variant holds it; false for unknown formats and malformed values.</summary>
        public static bool TryDecode(int format, IReadOnlyList<int> values, out DeformSpec spec)
        {
            spec = null;
            if (format != FormatVersion || values == null || values.Count < 2)
                return false;
            int flags = values[0];
            if ((flags & ~KnownFlags) != 0 || !IsDefined(values[1]))
                return false;
            bool linked = (flags & LinkedFlag) != 0;
            int expected = 2 + (linked ? 1 : 2) * CageOffsets.ValueCount;
            if (values.Count != expected)
                return false;

            var lot = CageOffsets.FromArray(Slice(values, 2));
            var building = linked ? lot : CageOffsets.FromArray(Slice(values, 2 + CageOffsets.ValueCount));
            spec = new DeformSpec(lot, building, (flags & FlatTerrainFlag) != 0, (BlendMode)values[1]).ForVariant();
            return true;
        }

        private static bool IsDefined(int mode) => mode == (int)BlendMode.All || mode == (int)BlendMode.None;

        private static int[] Slice(IReadOnlyList<int> values, int start)
        {
            var slice = new int[CageOffsets.ValueCount];
            for (int i = 0; i < slice.Length; i++)
                slice[i] = values[start + i];
            return slice;
        }
    }
}
