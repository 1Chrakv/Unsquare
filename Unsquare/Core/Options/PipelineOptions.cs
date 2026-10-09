using System;
using Unsquare.Core.Deformation;
using Unsquare.Core.Housekeeping;

namespace Unsquare.Core.Options
{
    /// <summary>
    /// Tunable settings of the variant pipeline: cage limits, attachments, rebake delay, pruning and lot sync
    /// tolerances. Immutable; the UI builds a new instance from the options page.
    /// </summary>
    public sealed class PipelineOptions
    {
        public static readonly PipelineOptions Default = new(CageLimits.Default, AttachmentOptions.Default, 0.4, true,
            (int)PrunePlanner.DefaultMaxUnusedAge.TotalDays, 1f, 1.5f);

        public PipelineOptions(CageLimits limits, AttachmentOptions attachments, double rebakeDelaySeconds,
            bool pruneUnused, int pruneAfterDays, float areaMatchMeters, float netMatchMeters)
        {
            if (!(rebakeDelaySeconds >= 0))
                throw new ArgumentException("Rebake delay must not be negative.", nameof(rebakeDelaySeconds));
            if (pruneAfterDays < 1)
                throw new ArgumentException("Prune age must be at least one day.", nameof(pruneAfterDays));
            if (!(areaMatchMeters > 0f))
                throw new ArgumentException("Area match distance must be positive.", nameof(areaMatchMeters));
            if (!(netMatchMeters > 0f))
                throw new ArgumentException("Net match distance must be positive.", nameof(netMatchMeters));
            Limits = limits ?? throw new ArgumentNullException(nameof(limits));
            Attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            RebakeDelaySeconds = rebakeDelaySeconds;
            PruneUnused = pruneUnused;
            PruneAfterDays = pruneAfterDays;
            AreaMatchMeters = areaMatchMeters;
            NetMatchMeters = netMatchMeters;
        }

        public CageLimits Limits { get; }
        public AttachmentOptions Attachments { get; }

        /// <summary>Quiet time after the last edit before a building is baked.</summary>
        public double RebakeDelaySeconds { get; }

        /// <summary>Whether variants unused for <see cref="PruneAfterDays"/> are deleted at startup.</summary>
        public bool PruneUnused { get; }
        public int PruneAfterDays { get; }
        public TimeSpan PruneAfter => TimeSpan.FromDays(PruneAfterDays);

        /// <summary>How far saved lot areas may be from their expected layout and still be matched.</summary>
        public float AreaMatchMeters { get; }

        /// <summary>How far saved lot nets may be from their expected layout and still be matched.</summary>
        public float NetMatchMeters { get; }

        public override string ToString() =>
            $"limits [{Limits}], attachments [{Attachments}], rebake {RebakeDelaySeconds} s, " +
            $"prune {(PruneUnused ? $"after {PruneAfterDays} d" : "off")}, match areas {AreaMatchMeters} m, nets {NetMatchMeters} m";
    }
}
