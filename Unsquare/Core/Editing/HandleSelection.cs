using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// What the editor works on inside a building: the pick mode (which kind the next click picks; default Edge)
    /// and the selected handle, if any. Immutable; a mode change keeps the selection until the next pick.
    /// </summary>
    public readonly struct HandleSelection : IEquatable<HandleSelection>
    {
        private readonly bool _pickKindSet;
        private readonly CageHandleKind _pickKind;

        private HandleSelection(CageHandleKind pickKind, bool hasHandle, CageHandle handle)
        {
            _pickKindSet = true;
            _pickKind = pickKind;
            HasHandle = hasHandle;
            Handle = handle;
        }

        public static HandleSelection Default => new(CageHandleKind.Edge, false, default);

        // default(HandleSelection) must also mean Edge, while the enum's first value is Corner.
        public CageHandleKind PickKind => _pickKindSet ? _pickKind : CageHandleKind.Edge;

        public bool HasHandle { get; }

        /// <summary>The selected handle; only meaningful when <see cref="HasHandle"/>.</summary>
        public CageHandle Handle { get; }

        public HandleSelection WithPickKind(CageHandleKind kind) => new(kind, HasHandle, Handle);

        /// <summary>Edge → Face → Corner → Edge.</summary>
        public HandleSelection CyclePickKind() => WithPickKind(PickKind switch
        {
            CageHandleKind.Edge => CageHandleKind.Face,
            CageHandleKind.Face => CageHandleKind.Corner,
            _ => CageHandleKind.Edge,
        });

        public HandleSelection Select(CageHandle handle) => new(PickKind, true, handle);

        public HandleSelection Clear() => new(PickKind, false, default);

        /// <summary>Next handle of the pick kind; starts at the first one if none of that kind is selected.</summary>
        public HandleSelection Next() =>
            Select(IsPickKindSelected
                ? CageHandleCursor.From(Handle).Next().Handle
                : new CageHandleCursor(PickKind, 0).Handle);

        /// <summary>Previous handle of the pick kind; starts at the last one if none of that kind is selected.</summary>
        public HandleSelection Previous() =>
            Select(IsPickKindSelected
                ? CageHandleCursor.From(Handle).Previous().Handle
                : new CageHandleCursor(PickKind, 0).Previous().Handle);

        private bool IsPickKindSelected => HasHandle && Handle.Kind == PickKind;

        public bool Equals(HandleSelection other) =>
            PickKind == other.PickKind && HasHandle == other.HasHandle && (!HasHandle || Handle == other.Handle);

        public override bool Equals(object obj) => obj is HandleSelection other && Equals(other);
        public override int GetHashCode() => (PickKind, HasHandle, HasHandle ? Handle : default).GetHashCode();
        public override string ToString() => $"pick {PickKind}, selected {(HasHandle ? Handle.ToString() : "none")}";
    }
}
