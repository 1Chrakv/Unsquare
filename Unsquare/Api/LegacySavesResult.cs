namespace Unsquare.Api
{
    /// <summary>
    /// What <see cref="IBuildingDeformService.RetireLegacyManifest"/> or
    /// <see cref="IBuildingDeformService.RestoreLegacyShapes"/> did.
    /// </summary>
    public readonly struct LegacySavesResult
    {
        private readonly bool _isRetire;

        private LegacySavesResult(bool isRetire, bool done, int count, string refusal)
        {
            _isRetire = isRetire;
            Done = done;
            Count = count;
            Refusal = refusal;
        }

        public bool Done { get; }

        /// <summary>Variants moved to the archive (retire) or back into the manifest (restore).</summary>
        public int Count { get; }

        /// <summary>Why nothing was done; null when <see cref="Done"/>.</summary>
        public string Refusal { get; }

        public static LegacySavesResult Retired(bool done, int count, string refusal) => new(true, done, count, refusal);
        public static LegacySavesResult Restored(bool done, int count, string refusal) => new(false, done, count, refusal);

        public override string ToString() => !Done ? $"not done: {Refusal}"
            : _isRetire ? $"{Count} old variants archived; from the next start, old saves load without them"
            : $"{Count} old variants restored; they load with the next save you open";
    }
}
