using System;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// How far cage corners may move and how much the cage may squeeze. The offset limit per axis is
    /// max(box size × <see cref="MaxOffsetFactor"/>, <see cref="MinOffsetLimitMeters"/>); vertically the largest
    /// box side counts.
    /// </summary>
    public sealed class CageLimits : IEquatable<CageLimits>
    {
        public static readonly CageLimits Default = new(2f, 5f, 0.05f);

        public CageLimits(float maxOffsetFactor, float minOffsetLimitMeters, float minVolumeRatio)
        {
            if (!(maxOffsetFactor > 0f))
                throw new ArgumentException("Offset factor must be positive.", nameof(maxOffsetFactor));
            if (!(minOffsetLimitMeters > 0f))
                throw new ArgumentException("Offset limit must be positive.", nameof(minOffsetLimitMeters));
            if (!(minVolumeRatio > 0f && minVolumeRatio < 1f))
                throw new ArgumentException("Volume ratio must be in (0, 1).", nameof(minVolumeRatio));
            MaxOffsetFactor = maxOffsetFactor;
            MinOffsetLimitMeters = minOffsetLimitMeters;
            MinVolumeRatio = minVolumeRatio;
        }

        /// <summary>Max corner offset per axis, relative to the box size (vertically: longest side).</summary>
        public float MaxOffsetFactor { get; }

        /// <summary>Lower bound of the offset limit so thin boxes can still be edited.</summary>
        public float MinOffsetLimitMeters { get; }

        /// <summary>Smallest allowed local volume ratio (Jacobian determinant); stops collapse and inversion.</summary>
        public float MinVolumeRatio { get; }

        public bool Equals(CageLimits other) => other != null &&
            MaxOffsetFactor.Equals(other.MaxOffsetFactor) && MinOffsetLimitMeters.Equals(other.MinOffsetLimitMeters) &&
            MinVolumeRatio.Equals(other.MinVolumeRatio);

        public override bool Equals(object obj) => obj is CageLimits other && Equals(other);
        public override int GetHashCode() => (MaxOffsetFactor, MinOffsetLimitMeters, MinVolumeRatio).GetHashCode();

        public override string ToString() =>
            $"offset ≤ max({MaxOffsetFactor}× size, {MinOffsetLimitMeters} m), volume ≥ {MinVolumeRatio:P0}";
    }
}
