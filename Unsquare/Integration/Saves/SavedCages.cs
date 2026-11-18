using System.Collections.Generic;
using Unsquare.Core.Cage;
using Unsquare.Core.Persistence;

namespace Unsquare.Integration.Saves
{
    /// <summary>Converts between <see cref="SavedCage"/> and <see cref="DeformSpec"/> through the Core codec.</summary>
    internal static class SavedCages
    {
        /// <summary>False if a value does not fit the component (offsets beyond ±3 km).</summary>
        public static bool TryFrom(DeformSpec spec, List<int> scratch, out SavedCage cage)
        {
            SavedCageCodec.Encode(spec, scratch);
            cage = new SavedCage { Format = SavedCageCodec.FormatVersion };
            if (scratch.Count > cage.Values.Capacity)
                return false;
            foreach (int value in scratch)
            {
                if (value < short.MinValue || value > short.MaxValue)
                    return false;
                cage.Values.Add((short)value);
            }
            return true;
        }

        public static bool TryRead(in SavedCage cage, List<int> scratch, out DeformSpec spec)
        {
            scratch.Clear();
            for (int i = 0; i < cage.Values.Length; i++)
                scratch.Add(cage.Values[i]);
            return SavedCageCodec.TryDecode(cage.Format, scratch, out spec);
        }
    }
}
