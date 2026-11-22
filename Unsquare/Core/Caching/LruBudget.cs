using System;
using System.Collections.Generic;

namespace Unsquare.Core.Caching
{
    /// <summary>
    /// Least-recently-used order of keys with a cost each; says which keys to drop to stay within a budget.
    /// Holds no values: the owner releases whatever the evicted keys stand for.
    /// </summary>
    public sealed class LruBudget<TKey>
    {
        private readonly LinkedList<Entry> _order = new(); // oldest first
        private readonly Dictionary<TKey, LinkedListNode<Entry>> _nodes;
        private long _budget;

        public LruBudget(long budget, IEqualityComparer<TKey> comparer = null)
        {
            Budget = budget;
            _nodes = new Dictionary<TKey, LinkedListNode<Entry>>(comparer ?? EqualityComparer<TKey>.Default);
        }

        public long Budget
        {
            get => _budget;
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Budget must be positive.", nameof(value));
                _budget = value;
            }
        }

        public int Count => _nodes.Count;
        public long TotalCost { get; private set; }

        public bool Contains(TKey key) => _nodes.ContainsKey(key);

        /// <summary>Adds the key or makes it the most recent; a repeated touch replaces its cost.</summary>
        public void Touch(TKey key, long cost)
        {
            if (cost < 0)
                throw new ArgumentException("Cost must not be negative.", nameof(cost));
            if (_nodes.TryGetValue(key, out var node))
            {
                TotalCost -= node.Value.Cost;
                _order.Remove(node);
                node.Value = new Entry(key, cost);
                _order.AddLast(node);
            }
            else
                _nodes[key] = _order.AddLast(new Entry(key, cost));
            TotalCost += cost;
        }

        /// <summary>Removes the oldest keys until the total fits and adds them to <paramref name="evicted"/>.</summary>
        /// <remarks>The most recent key always stays, even alone over the budget: it is the one in use.</remarks>
        public void CollectOverBudget(List<TKey> evicted)
        {
            while (TotalCost > _budget && _order.Count > 1)
            {
                var oldest = _order.First.Value;
                Remove(oldest.Key);
                evicted.Add(oldest.Key);
            }
        }

        public bool Remove(TKey key)
        {
            if (!_nodes.TryGetValue(key, out var node))
                return false;
            _nodes.Remove(key);
            _order.Remove(node);
            TotalCost -= node.Value.Cost;
            return true;
        }

        public void Clear()
        {
            _nodes.Clear();
            _order.Clear();
            TotalCost = 0;
        }

        private readonly struct Entry
        {
            public Entry(TKey key, long cost)
            {
                Key = key;
                Cost = cost;
            }

            public TKey Key { get; }
            public long Cost { get; }
        }
    }
}
