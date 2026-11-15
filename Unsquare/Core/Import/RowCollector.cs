using System;
using System.Collections.Generic;

namespace Unsquare.Core.Import
{
    /// <summary>Reads the joined neighbours of a building in a row; false if the building is unknown.</summary>
    public delegate bool TryNeighbours<T>(T building, out T left, out T right);

    /// <summary>
    /// Collects a row of joined buildings (e.g. Placemaker's row houses) by walking neighbour links, so the whole
    /// row can be converted at once.
    /// </summary>
    public static class RowCollector
    {
        /// <summary>
        /// The start and every building reachable over neighbour links, nearest first, at most
        /// <paramref name="maxCount"/>. <paramref name="none"/> marks a missing neighbour.
        /// </summary>
        public static List<T> Collect<T>(T start, T none, TryNeighbours<T> neighbours, int maxCount)
        {
            if (neighbours == null)
                throw new ArgumentNullException(nameof(neighbours));
            if (maxCount < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCount));
            var comparer = EqualityComparer<T>.Default;
            var row = new List<T> { start };
            var seen = new HashSet<T>(comparer) { start, none };
            for (int i = 0; i < row.Count && row.Count < maxCount; i++)
            {
                if (!neighbours(row[i], out var left, out var right))
                    continue;
                if (seen.Add(left))
                    row.Add(left);
                if (row.Count < maxCount && seen.Add(right))
                    row.Add(right);
            }
            return row;
        }
    }
}
