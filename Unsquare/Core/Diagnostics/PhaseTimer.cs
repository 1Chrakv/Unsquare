using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Unsquare.Core.Diagnostics
{
    /// <summary>
    /// Adds up the time spent per named phase and plain counters, both in the order they first appear.
    /// Scopes must not be nested: a nested phase would be counted twice.
    /// </summary>
    public sealed class PhaseTimer
    {
        private readonly Func<long> _ticks;
        private readonly double _millisecondsPerTick;
        private readonly List<string> _phases = new();
        private readonly Dictionary<string, long> _elapsed = new(StringComparer.Ordinal);
        private readonly List<string> _counters = new();
        private readonly Dictionary<string, long> _counts = new(StringComparer.Ordinal);

        public PhaseTimer() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency)
        {
        }

        /// <param name="ticks">Monotonic clock.</param>
        public PhaseTimer(Func<long> ticks, long ticksPerSecond)
        {
            if (ticksPerSecond <= 0)
                throw new ArgumentException("Clock frequency must be positive.", nameof(ticksPerSecond));
            _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
            _millisecondsPerTick = 1000.0 / ticksPerSecond;
        }

        /// <summary>Times the phase until the returned scope is disposed.</summary>
        public Scope Measure(string phase) => new(this, phase, _ticks());

        public void Count(string counter, int amount)
        {
            if (!_counts.TryGetValue(counter, out long total))
                _counters.Add(counter);
            _counts[counter] = total + amount;
        }

        public double Milliseconds(string phase) =>
            _elapsed.TryGetValue(phase, out long ticks) ? ticks * _millisecondsPerTick : 0.0;

        public long Total(string counter) => _counts.TryGetValue(counter, out long total) ? total : 0;

        public void Reset()
        {
            _phases.Clear();
            _elapsed.Clear();
            _counters.Clear();
            _counts.Clear();
        }

        /// <summary>E.g. "obtain 12.3, deform 38.0 ms; 48210 vertices, 3 meshes".</summary>
        public string Summary()
        {
            var text = new StringBuilder();
            for (int i = 0; i < _phases.Count; i++)
            {
                text.Append(i == 0 ? string.Empty : ", ").Append(_phases[i]).Append(' ')
                    .Append(Milliseconds(_phases[i]).ToString("0.0", CultureInfo.InvariantCulture));
            }
            text.Append(_phases.Count == 0 ? "nothing timed" : " ms");
            for (int i = 0; i < _counters.Count; i++)
            {
                text.Append(i == 0 ? "; " : ", ").Append(_counts[_counters[i]].ToString(CultureInfo.InvariantCulture))
                    .Append(' ').Append(_counters[i]);
            }
            return text.ToString();
        }

        private void Add(string phase, long ticks)
        {
            if (!_elapsed.TryGetValue(phase, out long total))
                _phases.Add(phase);
            _elapsed[phase] = total + ticks;
        }

        /// <summary>One running measurement; dispose it to add its time to the phase.</summary>
        public readonly struct Scope : IDisposable
        {
            private readonly PhaseTimer _timer;
            private readonly string _phase;
            private readonly long _start;

            internal Scope(PhaseTimer timer, string phase, long start)
            {
                _timer = timer;
                _phase = phase;
                _start = start;
            }

            public void Dispose() => _timer?.Add(_phase, _timer._ticks() - _start);
        }
    }
}
