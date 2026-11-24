using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Mathematics;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// A building named by prefab and ground position ("<c>Prefab@x,z</c>"), for tests that outlive entity ids:
    /// ids change between loads, a building's prefab and place don't. Variants match by their base name.
    /// </summary>
    public readonly struct BuildingRef : IEquatable<BuildingRef>
    {
        public const float DefaultTolerance = 2f;

        public BuildingRef(string prefab, float2 position)
        {
            if (string.IsNullOrEmpty(prefab))
                throw new ArgumentException("A building reference needs a prefab name.", nameof(prefab));
            Prefab = prefab;
            Position = position;
        }

        public string Prefab { get; }

        /// <summary>World x and z of the building's position.</summary>
        public float2 Position { get; }

        public static bool TryParse(string text, out BuildingRef reference)
        {
            reference = default;
            var at = text?.LastIndexOf('@') ?? -1;
            if (at <= 0)
                return false;
            var parts = text.Substring(at + 1).Split(',');
            if (parts.Length != 2
                || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;
            reference = new BuildingRef(text.Substring(0, at), new float2(x, z));
            return true;
        }

        /// <summary>
        /// Index of the candidate with this prefab (or a variant of it) nearest to the position, within
        /// <paramref name="tolerance"/> metres; -1 if none.
        /// </summary>
        public int FindIn(IReadOnlyList<(string Prefab, float2 Position)> candidates, float tolerance = DefaultTolerance)
        {
            var best = -1;
            var bestDistance = tolerance;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!SamePrefab(candidates[i].Prefab))
                    continue;
                var distance = math.distance(candidates[i].Position, Position);
                if (distance > bestDistance)
                    continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        public bool SamePrefab(string prefab) =>
            prefab == Prefab || BaseName(prefab) == BaseName(Prefab);

        private static string BaseName(string prefab) =>
            Naming.VariantNaming.TryParse(prefab, out var baseName) ? baseName : prefab;

        public bool Equals(BuildingRef other) => Prefab == other.Prefab && Position.Equals(other.Position);
        public override bool Equals(object obj) => obj is BuildingRef other && Equals(other);
        public override int GetHashCode() => (Prefab, Position).GetHashCode();

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}@{1:0.0},{2:0.0}",
            Prefab, Position.x, Position.y);
    }
}
