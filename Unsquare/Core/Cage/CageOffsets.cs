using System;
using System.Text;
using Unity.Mathematics;

namespace Unsquare.Core.Cage
{
    /// <summary>
    /// Immutable displacement of the 8 cage corners, quantized to <see cref="StepMeters"/>.
    /// Quantization keeps variant names deterministic and the number of variants bounded.
    /// </summary>
    public sealed class CageOffsets : IEquatable<CageOffsets>
    {
        /// <summary>Grid size in meters. Part of the permanent naming scheme, never change it.</summary>
        public const float StepMeters = 0.1f;
        public const int ValueCount = CageTopology.CornerCount * 3;

        public static readonly CageOffsets Identity = new(new int3[CageTopology.CornerCount]);

        private readonly int3[] _steps;

        private CageOffsets(int3[] steps)
        {
            _steps = steps;
        }

        public bool IsIdentity
        {
            get
            {
                for (int i = 0; i < _steps.Length; i++)
                {
                    if (math.any(_steps[i] != int3.zero))
                        return false;
                }
                return true;
            }
        }

        /// <summary>Offset of a corner in grid steps.</summary>
        public int3 Steps(int corner) => _steps[corner];

        /// <summary>Offset of a corner in meters.</summary>
        public float3 Meters(int corner) => (float3)_steps[corner] * StepMeters;

        public static int Quantize(float meters) => (int)math.round(meters / StepMeters);
        public static int3 Quantize(float3 meters) => (int3)math.round(meters / StepMeters);

        /// <summary>Creates offsets from per-corner meter values (rounded to the grid).</summary>
        public static CageOffsets FromMeters(float3[] cornerMeters)
        {
            if (cornerMeters == null || cornerMeters.Length != CageTopology.CornerCount)
                throw new ArgumentException($"Expected {CageTopology.CornerCount} corners.", nameof(cornerMeters));
            var steps = new int3[CageTopology.CornerCount];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = Quantize(cornerMeters[i]);
            return new CageOffsets(steps);
        }

        /// <summary>Creates offsets from the flat x,y,z-per-corner step array of <see cref="ToArray"/>.</summary>
        public static CageOffsets FromArray(int[] values)
        {
            if (values == null || values.Length != ValueCount)
                throw new ArgumentException($"Expected {ValueCount} values.", nameof(values));
            var steps = new int3[CageTopology.CornerCount];
            for (int i = 0; i < steps.Length; i++)
                steps[i] = new int3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
            return new CageOffsets(steps);
        }

        public int[] ToArray()
        {
            var values = new int[ValueCount];
            for (int i = 0; i < _steps.Length; i++)
            {
                values[i * 3] = _steps[i].x;
                values[i * 3 + 1] = _steps[i].y;
                values[i * 3 + 2] = _steps[i].z;
            }
            return values;
        }

        /// <summary>
        /// Returns offsets with every corner of the handle shifted by a delta in meters.
        /// For drags, pass the total delta since drag start applied to the start offsets to avoid rounding drift.
        /// </summary>
        public CageOffsets Move(CageHandle handle, float3 deltaMeters)
        {
            int3 delta = Quantize(deltaMeters);
            if (math.all(delta == int3.zero))
                return this;
            var steps = (int3[])_steps.Clone();
            var corners = CageTopology.CornersOf(handle);
            for (int i = 0; i < corners.Count; i++)
                steps[corners[i]] += delta;
            return new CageOffsets(steps);
        }

        /// <summary>Returns offsets with a single corner set to an absolute value in steps.</summary>
        public CageOffsets WithCornerSteps(int corner, int3 steps)
        {
            if (_steps[corner].Equals(steps))
                return this;
            var copy = (int3[])_steps.Clone();
            copy[corner] = steps;
            return new CageOffsets(copy);
        }

        /// <summary>Canonical text form used for hashing, e.g. "0,0,0;1,0,-2;...".</summary>
        public string ToCanonicalString()
        {
            var sb = new StringBuilder(ValueCount * 3);
            for (int i = 0; i < _steps.Length; i++)
            {
                if (i > 0)
                    sb.Append(';');
                sb.Append(_steps[i].x).Append(',').Append(_steps[i].y).Append(',').Append(_steps[i].z);
            }
            return sb.ToString();
        }

        public bool Equals(CageOffsets other)
        {
            if (ReferenceEquals(this, other))
                return true;
            if (other is null)
                return false;
            for (int i = 0; i < _steps.Length; i++)
            {
                if (!_steps[i].Equals(other._steps[i]))
                    return false;
            }
            return true;
        }

        public override bool Equals(object obj) => obj is CageOffsets other && Equals(other);

        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < _steps.Length; i++)
                hash = hash * 31 + _steps[i].GetHashCode();
            return hash;
        }

        public override string ToString() => ToCanonicalString();
    }
}
