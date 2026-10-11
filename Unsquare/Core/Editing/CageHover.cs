using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Editing
{
    /// <summary>What the mouse is over: nothing, a cage handle or a gizmo axis. Immutable.</summary>
    public readonly struct CageHover : IEquatable<CageHover>
    {
        private CageHover(bool hasHandle, CageHandle handle, int axis)
        {
            HasHandle = hasHandle;
            Handle = handle;
            Axis = axis;
        }

        public static CageHover None { get; } = new(false, default, -1);

        public bool HasHandle { get; }
        public CageHandle Handle { get; }

        /// <summary>Gizmo axis (0..2), or -1.</summary>
        public int Axis { get; }

        public bool HasAxis => Axis >= 0;

        public static CageHover OnHandle(CageHandle handle) => new(true, handle, -1);

        public static CageHover OnAxis(int axis)
        {
            if (axis < 0 || axis >= GizmoAxes.AxisCount)
                throw new ArgumentOutOfRangeException(nameof(axis), axis, null);
            return new CageHover(false, default, axis);
        }

        public bool Equals(CageHover other) =>
            HasHandle == other.HasHandle && Handle.Equals(other.Handle) && Axis == other.Axis;

        public override bool Equals(object obj) => obj is CageHover other && Equals(other);
        public override int GetHashCode() => (HasHandle, Handle, Axis).GetHashCode();
        public override string ToString() => HasHandle ? Handle.ToString() : HasAxis ? $"Axis{Axis}" : "None";
    }
}
