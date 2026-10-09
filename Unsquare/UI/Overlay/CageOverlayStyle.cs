using UnityEngine;
using Unsquare.Api;

namespace Unsquare.UI.Overlay
{
    /// <summary>
    /// Colours and sizes of the cage overlay. Kept apart from drawing so later states
    /// (hover, drag, multi-select) only add entries here.
    /// </summary>
    public sealed class CageOverlayStyle
    {
        public static CageOverlayStyle Default { get; } = new(0.1f, 0.8f, 1.8f, 2f);

        public CageOverlayStyle(float lineRadius, float handleDiameter, float selectedHandleDiameter, float axisLength)
        {
            LineRadius = lineRadius;
            HandleDiameter = handleDiameter;
            SelectedHandleDiameter = selectedHandleDiameter;
            AxisLength = axisLength;
        }

        public Color RestBox { get; } = new(1f, 1f, 1f, 0.35f);
        public Color Handle { get; } = new(1f, 1f, 1f, 0.8f);
        public Color SelectedHandle { get; } = new(1f, 0.55f, 0f, 1f);
        public Color AxisX { get; } = new(0.95f, 0.2f, 0.2f, 1f);
        public Color AxisY { get; } = new(0.2f, 0.9f, 0.2f, 1f);
        public Color AxisZ { get; } = new(0.25f, 0.45f, 1f, 1f);

        /// <summary>Lot cage whose ground outline leaves the original lot (allowed, but may overlap neighbours).</summary>
        public Color LotWarning { get; } = new(1f, 0.2f, 0.8f, 0.9f);

        /// <summary>Lines are drawn as cylinders; this is their radius.</summary>
        public float LineRadius { get; }
        public float DashLength { get; } = 1f;
        public float GapLength { get; } = 0.6f;
        public float HandleDiameter { get; }
        public float SelectedHandleDiameter { get; }
        public float AxisLength { get; }

        public Color Cage(DeformStatus status) => status switch
        {
            DeformStatus.Applied => new Color(0.3f, 1f, 0.4f, 0.9f),
            DeformStatus.Pending => new Color(1f, 0.85f, 0.2f, 0.9f),
            DeformStatus.Applying => new Color(1f, 0.85f, 0.2f, 0.9f),
            DeformStatus.Failed => new Color(1f, 0.25f, 0.25f, 0.9f),
            _ => new Color(1f, 1f, 1f, 0.9f),
        };
    }
}
