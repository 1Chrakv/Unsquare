using System;
using System.Collections.Generic;

namespace Unsquare.Core.Scheduling
{
    /// <summary>
    /// Keeps only the latest request per key and releases it once no newer request arrived for the delay.
    /// Time is passed in explicitly (seconds) so callers choose the clock and tests stay deterministic.
    /// </summary>
    public sealed class Debouncer<TKey, TValue>
    {
        private readonly Dictionary<TKey, (TValue Value, double Due)> _pending;
        private readonly List<TKey> _scratch = new();

        private double _delaySeconds;

        public Debouncer(double delaySeconds, IEqualityComparer<TKey> comparer = null)
        {
            DelaySeconds = delaySeconds;
            _pending = new Dictionary<TKey, (TValue, double)>(comparer ?? EqualityComparer<TKey>.Default);
        }

        /// <summary>Delay for new requests; already pending ones keep their due time.</summary>
        public double DelaySeconds
        {
            get => _delaySeconds;
            set => _delaySeconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
        }
        public int Count => _pending.Count;

        /// <summary>Replaces any pending value for the key and restarts its delay.</summary>
        public void Request(TKey key, TValue value, double now) => _pending[key] = (value, now + DelaySeconds);

        public bool TryPeek(TKey key, out TValue value)
        {
            bool found = _pending.TryGetValue(key, out var entry);
            value = found ? entry.Value : default;
            return found;
        }

        public bool Cancel(TKey key) => _pending.Remove(key);

        public void Clear() => _pending.Clear();

        /// <summary>Moves all due entries into <paramref name="due"/> and returns how many were added.</summary>
        public int CollectDue(double now, List<KeyValuePair<TKey, TValue>> due)
        {
            if (due == null)
                throw new ArgumentNullException(nameof(due));
            _scratch.Clear();
            foreach (var pair in _pending)
            {
                if (pair.Value.Due <= now)
                    _scratch.Add(pair.Key);
            }
            foreach (var key in _scratch)
            {
                due.Add(new KeyValuePair<TKey, TValue>(key, _pending[key].Value));
                _pending.Remove(key);
            }
            return _scratch.Count;
        }
    }
}
