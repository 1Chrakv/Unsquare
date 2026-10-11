using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>One undoable cage edit: a building's target offsets before and after the change.</summary>
    public readonly struct CageEdit<TKey>
    {
        public CageEdit(TKey building, CageOffsets before, CageOffsets after)
        {
            Building = building;
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
        }

        public TKey Building { get; }
        public CageOffsets Before { get; }
        public CageOffsets After { get; }
    }
}
