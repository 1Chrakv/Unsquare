using System;
using Unsquare.Core.Deformation;

namespace Unsquare.Core.Options
{
    /// <summary>
    /// Tunable settings of the variant pipeline: cage limits, attachments, rebake delay, geometry cache size, source
    /// mesh budget, lot sync tolerances and level-ups. Immutable; the UI builds a new instance from the options page.
    /// </summary>
    public sealed class PipelineOptions
    {
        public static readonly PipelineOptions Default = new(CageLimits.Default, AttachmentOptions.Default, 0.2, 2f, 1f,
            1f, true, 1_000_000);

        private const long BytesPerGigabyte = 1024L * 1024 * 1024;

        public PipelineOptions(CageLimits limits, AttachmentOptions attachments, double rebakeDelaySeconds,
            float geometryCacheGigabytes, float areaMatchMeters, float coarseLodTwistMeters, bool keepOnLevelUp,
            long sourceMeshBudgetVertices)
        {
            if (!(rebakeDelaySeconds >= 0))
                throw new ArgumentException("Rebake delay must not be negative.", nameof(rebakeDelaySeconds));
            if (!(geometryCacheGigabytes > 0f))
                throw new ArgumentException("Geometry cache size must be positive.", nameof(geometryCacheGigabytes));
            if (!(areaMatchMeters > 0f))
                throw new ArgumentException("Area match distance must be positive.", nameof(areaMatchMeters));
            if (!(coarseLodTwistMeters >= 0f))
                throw new ArgumentException("Coarse LOD twist must not be negative.", nameof(coarseLodTwistMeters));
            if (sourceMeshBudgetVertices <= 0)
                throw new ArgumentException("Source mesh budget must be positive.", nameof(sourceMeshBudgetVertices));
            Limits = limits ?? throw new ArgumentNullException(nameof(limits));
            Attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
            RebakeDelaySeconds = rebakeDelaySeconds;
            GeometryCacheGigabytes = geometryCacheGigabytes;
            AreaMatchMeters = areaMatchMeters;
            CoarseLodTwistMeters = coarseLodTwistMeters;
            KeepOnLevelUp = keepOnLevelUp;
            SourceMeshBudgetVertices = sourceMeshBudgetVertices;
        }

        public CageLimits Limits { get; }
        public AttachmentOptions Attachments { get; }

        /// <summary>Quiet time after the last edit before a building is baked.</summary>
        public double RebakeDelaySeconds { get; }

        /// <summary>Baked geometry beyond this size is evicted at startup, least recently used first.</summary>
        /// <remarks>Assets that legacy manifest entries claim are outside the cache and don't count.</remarks>
        public float GeometryCacheGigabytes { get; }
        public long GeometryCacheBytes => (long)(GeometryCacheGigabytes * BytesPerGigabyte);

        /// <summary>How far saved lot areas may be from their expected layout and still be matched.</summary>
        public float AreaMatchMeters { get; }

        /// <summary>
        /// Cages twisting more than this (<see cref="CageTwist"/>) drop the coarsest LOD: its large triangles
        /// can't follow the bend, so the next finer LOD is drawn out to the culling distance instead.
        /// </summary>
        public float CoarseLodTwistMeters { get; }

        /// <summary>Whether a deformed growable that levels up gets its cage carried over to the new building.</summary>
        public bool KeepOnLevelUp { get; }

        /// <summary>Vertices of base meshes kept loaded between bakes; the least recently used beyond it are released.</summary>
        /// <remarks>Not a setting: it spares repeated edits a reload while bounding memory over many buildings.</remarks>
        public long SourceMeshBudgetVertices { get; }

        public override string ToString() =>
            $"limits [{Limits}], attachments [{Attachments}], rebake {RebakeDelaySeconds} s, " +
            $"geometry cache {GeometryCacheGigabytes} GB, match areas {AreaMatchMeters} m, " +
            $"coarse LOD twist {CoarseLodTwistMeters} m, keep on level-up {KeepOnLevelUp}, " +
            $"source meshes {SourceMeshBudgetVertices} vertices";
    }
}
