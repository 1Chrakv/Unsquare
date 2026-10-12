using System;
using System.Collections.Generic;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Undo and redo stacks of cage edits across all buildings. Recording clears the redo stack; beyond
    /// <see cref="Capacity"/> the oldest edits are dropped.
    /// </summary>
    public sealed class EditHistory<TKey> where TKey : IEquatable<TKey>
    {
        private readonly List<CageEdit<TKey>> _undo = new();
        private readonly List<CageEdit<TKey>> _redo = new();
        private int _capacity;

        public EditHistory(int capacity)
        {
            Capacity = capacity;
        }

        /// <summary>Maximum number of undo steps; lowering it drops the oldest ones.</summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value < 1)
                    throw new ArgumentException("Capacity must be at least 1.", nameof(value));
                _capacity = value;
                Trim(_undo);
                Trim(_redo);
            }
        }

        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Adds an edit unless it changed nothing.</summary>
        public void Record(TKey building, DeformSpec before, DeformSpec after)
        {
            var edit = new CageEdit<TKey>(building, before, after);
            if (before.Equals(after))
                return;
            _redo.Clear();
            _undo.Add(edit);
            Trim(_undo);
        }

        /// <summary>Takes the newest edit; the caller restores <see cref="CageEdit{TKey}.Before"/>.</summary>
        public bool TryUndo(out CageEdit<TKey> edit) => TryMove(_undo, _redo, out edit);

        /// <summary>Takes the newest undone edit; the caller restores <see cref="CageEdit{TKey}.After"/>.</summary>
        public bool TryRedo(out CageEdit<TKey> edit) => TryMove(_redo, _undo, out edit);

        /// <summary>Drops every edit of one building, e.g. when it no longer exists.</summary>
        public void Forget(TKey building)
        {
            _undo.RemoveAll(edit => edit.Building.Equals(building));
            _redo.RemoveAll(edit => edit.Building.Equals(building));
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        private static bool TryMove(List<CageEdit<TKey>> from, List<CageEdit<TKey>> to, out CageEdit<TKey> edit)
        {
            edit = default;
            if (from.Count == 0)
                return false;
            edit = from[from.Count - 1];
            from.RemoveAt(from.Count - 1);
            to.Add(edit);
            return true;
        }

        private void Trim(List<CageEdit<TKey>> stack)
        {
            if (stack.Count > _capacity)
                stack.RemoveRange(0, stack.Count - _capacity);
        }
    }
}
