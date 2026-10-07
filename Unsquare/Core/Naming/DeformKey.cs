using System;
using System.Globalization;
using Unity.Mathematics;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Naming
{
    /// <summary>
    /// Identifies baked geometry: format version + box (cm) + offsets. The box is included so a changed
    /// base mesh rebakes, while variant prefab names (offsets only) stay valid.
    /// </summary>
    public static class DeformKey
    {
        /// <summary>Bump to invalidate every baked asset, e.g. after changing the vertex math.</summary>
        public const int FormatVersion = 1;

        public static string Create(CageBox box, CageOffsets offsets)
        {
            if (offsets == null)
                throw new ArgumentNullException(nameof(offsets));
            return $"cage|v{FormatVersion}|{Centimeters(box.Min)}|{Centimeters(box.Max)}|{offsets.ToCanonicalString()}";
        }

        private static string Centimeters(float3 meters)
        {
            int3 cm = (int3)math.round(meters * 100f);
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", cm.x, cm.y, cm.z);
        }
    }
}
