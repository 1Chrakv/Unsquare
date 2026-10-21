namespace Unsquare.Api
{
    /// <summary>What <see cref="IBuildingDeformService.ImportFlexIt"/> did with the city's FlexIt buildings.</summary>
    public readonly struct FlexItImportResult
    {
        public FlexItImportResult(bool noCity, bool flexItMissing, int converted, int approximated, int clamped,
            int reset, int failed)
        {
            NoCity = noCity;
            FlexItMissing = flexItMissing;
            Converted = converted;
            Approximated = approximated;
            Clamped = clamped;
            Reset = reset;
            Failed = failed;
        }

        /// <summary>No city loaded; nothing was done.</summary>
        public bool NoCity { get; }

        /// <summary>FlexIt is not running, so its buildings cannot be read; nothing was done.</summary>
        public bool FlexItMissing { get; }

        /// <summary>Buildings requested as Unsquare variants (including <see cref="Approximated"/> and <see cref="Clamped"/>).</summary>
        public int Converted { get; }

        /// <summary>Converted corner fits: the closest single cage, without the fold at the corner.</summary>
        public int Approximated { get; }

        /// <summary>Converted, but beyond the cage limits: the shape was clamped to them.</summary>
        public int Clamped { get; }

        /// <summary>Arched and bent FlexIt variants moved back to their original prefab.</summary>
        public int Reset { get; }

        /// <summary>Buildings left as they were, or on their original prefab when the cage was rejected; see the log.</summary>
        public int Failed { get; }

        public override string ToString() => NoCity ? "no city loaded"
            : FlexItMissing ? "FlexIt is not running"
            : $"{Converted} converted ({Approximated} approximated, {Clamped} clamped), {Reset} curved reset, " +
              $"{Failed} failed";
    }
}
