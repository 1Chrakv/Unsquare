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
            (int)PrunePlanner.DefaultMaxUnusedAge.TotalDays, 1f, 1f);

        public PipelineOptions(CageLimits limits, AttachmentOptions attachments, double rebakeDelaySeconds,
            bool pruneUnused, int pruneAfterDays, float areaMatchMeters, float coarseLodTwistMeters)
        {
            if (!(rebakeDelaySeconds >= 0))
                throw new ArgumentException("Rebake delay must not be negative.", nameof(rebakeDelaySeconds));
            if (pruneAfterDays < 1)
                throw new ArgumentException("Prune age must be at least one day.", nameof(pruneAfterDays));
            if (!(areaMatchMeters > 0f))
                throw new ArgumentException("Area match distance must be positive.", nameof(areaMatchMeters));
            if (!(coarseLodTwistMeters >= 0f))
                throw new ArgumentException("Coarse LOD twist must not be negative.", nameof(coarseLodTwistMeters));
            Limits = limits ?? throw new ArgumentNullException(nameof(limits));
            Attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            RebakeDelaySeconds = rebakeDelaySeconds;
            PruneUnused = pruneUnused;
            PruneAfterDays = pruneAfterDays;
            AreaMatchMeters = areaMatchMeters;
            CoarseLodTwistMeters = coarseLodTwistMeters;
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

        /// <summary>
        /// Cages twisting more than this (<see cref="CageTwist"/>) drop the coarsest LOD: its large triangles
        /// can't follow the bend, so the next finer LOD is drawn out to the culling distance instead.
        /// </summary>
        public float CoarseLodTwistMeters { get; }

        public override string ToString() =>
            $"limits [{Limits}], attachments [{Attachments}], rebake {RebakeDelaySeconds} s, " +
            $"prune {(PruneUnused ? $"after {PruneAfterDays} d" : "off")}, match areas {AreaMatchMeters} m, " +
            $"coarse LOD twist {CoarseLodTwistMeters} m";
    }
}
