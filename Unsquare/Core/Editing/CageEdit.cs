using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>One undoable cage edit: a building's target deformation before and after the change.</summary>
    public readonly struct CageEdit<TKey>
    {
        public CageEdit(TKey building, DeformSpec before, DeformSpec after)
        {
            Building = building;
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
        }

        public TKey Building { get; }
        public DeformSpec Before { get; }
        public DeformSpec After { get; }
    }
}
