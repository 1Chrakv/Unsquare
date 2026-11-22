namespace Unsquare.Api
{
    /// <summary>
    /// What <see cref="IBuildingDeformService.RetireLegacyManifest"/> or
    /// <see cref="IBuildingDeformService.RestoreLegacyShapes"/> did.
    /// </summary>
    public readonly struct LegacySavesResult
    {
        private readonly Kind _kind;

        private LegacySavesResult(Kind kind, bool done, int count, string refusal)
        {
            _kind = kind;
            Done = done;
            Count = count;
            Refusal = refusal;
        }

        public bool Done { get; }

        /// <summary>Variants moved to the archive (retire) or back into the manifest (restore).</summary>
        public int Count { get; }

        /// <summary>Why nothing was done; null when <see cref="Done"/>.</summary>
        public string Refusal { get; }

        public static LegacySavesResult Retired(bool done, int count, string refusal) =>
            new(Kind.Retire, done, count, refusal);

        public static LegacySavesResult Restored(bool done, int count, string refusal) =>
            new(Kind.Restore, done, count, refusal);

        public override string ToString() => !Done
            ? $"not done: {Refusal}"
            : _kind == Kind.Retire
                ? $"{Count} old variants archived; from the next start, old saves load without them"
                : $"{Count} old variants restored; they load with the next save you open";

        private enum Kind
        {
            Retire,
            Restore,
        }
    }
}
