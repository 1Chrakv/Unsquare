using System.Collections.Generic;

namespace Unsquare.Api
{
    /// <summary>
    /// What <see cref="IBuildingDeformService.RetireLegacyManifest"/> or
    /// <see cref="IBuildingDeformService.RestoreLegacyShapes"/> did.
    /// </summary>
    public readonly struct LegacySavesResult
    {
        private const int NamesShown = 5; // the log lists all

        private readonly Kind _kind;

        private LegacySavesResult(Kind kind, bool done, int count, string refusal, string warning)
        {
            _kind = kind;
            Done = done;
            Count = count;
            Refusal = refusal;
            Warning = warning;
        }

        public bool Done { get; }

        /// <summary>Variants moved to the archive (retire) or back into the manifest (restore).</summary>
        public int Count { get; }

        /// <summary>Why nothing was done; null when <see cref="Done"/>.</summary>
        public string Refusal { get; }

        /// <summary>Buildings of the loaded city the player must deform again; null when there are none.</summary>
        public string Warning { get; }

        public static LegacySavesResult Retired(bool done, int count, IReadOnlyList<string> failedBuildings,
            string refusal) =>
            new(Kind.Retire, done, count, refusal, done ? FailedWarning(failedBuildings) : null);

        public static LegacySavesResult Restored(bool done, int count, string refusal) =>
            new(Kind.Restore, done, count, refusal, null);

        /// <summary>What happened, without the <see cref="Warning"/>.</summary>
        public string Summary => !Done
            ? $"not done: {Refusal}"
            : _kind == Kind.Retire
                ? $"{Count} old variants archived; from the next start, old saves load without them"
                : $"{Count} old variants restored; they load with the next save you open";

        public override string ToString() => Warning == null ? Summary : $"{Summary}. {Warning}";

        private static string FailedWarning(IReadOnlyList<string> buildings)
        {
            if (buildings == null || buildings.Count == 0)
                return null;
            var shown = new List<string>();
            for (int i = 0; i < buildings.Count && i < NamesShown; i++)
                shown.Add(buildings[i]);
            string more = buildings.Count > NamesShown ? $" and {buildings.Count - NamesShown} more (see the log)" : "";
            return $"These buildings of this city failed to load their old shape: {string.Join(", ", shown)}{more}. " +
                   "Deform them again: old shapes go away with the old save format.";
        }

        private enum Kind
        {
            Retire,
            Restore,
        }
    }
}
