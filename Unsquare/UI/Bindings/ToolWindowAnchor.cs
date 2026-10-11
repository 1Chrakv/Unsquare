using System;
using Colossal.UI.Binding;

namespace Unsquare.UI.Bindings
{
    /// <summary>
    /// Screen position of the floating tool window: 0..1 from the top left, so the UI module needs no
    /// knowledge of resolution or UI scale. Field names must match tool-window.tsx.
    /// </summary>
    internal readonly struct ToolWindowAnchor : IJsonWritable, IEquatable<ToolWindowAnchor>
    {
        public ToolWindowAnchor(float x, float y)
        {
            Visible = true;
            X = x;
            Y = y;
        }

        public static ToolWindowAnchor Hidden => default;

        public bool Visible { get; }
        public float X { get; }
        public float Y { get; }

        public void Write(IJsonWriter writer)
        {
            writer.TypeBegin(typeof(ToolWindowAnchor).FullName);
            writer.PropertyName("visible");
            writer.Write(Visible);
            writer.PropertyName("x");
            writer.Write(X);
            writer.PropertyName("y");
            writer.Write(Y);
            writer.TypeEnd();
        }

        public bool Equals(ToolWindowAnchor other) => Visible == other.Visible && X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is ToolWindowAnchor other && Equals(other);
        public override int GetHashCode() => (Visible, X, Y).GetHashCode();
    }
}
