using UnityEngine;
using Unsquare.Api;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Overlay
{
    /// <summary>
    /// Colours and sizes of the cage overlay and the move gizmo. Kept apart from drawing so new states
    /// only add entries here.
    /// </summary>
    public sealed class CageOverlayStyle
    {
        public static CageOverlayStyle Default { get; } = new(0.1f, 0.8f, 1.8f, 1f, 0.06f);

        public CageOverlayStyle(float lineRadius, float handleDiameter, float selectedHandleDiameter,
            float axisLength, float gizmoScreenSize)
        {
            LineRadius = lineRadius;
            HandleDiameter = handleDiameter;
            SelectedHandleDiameter = selectedHandleDiameter;
            AxisLength = axisLength;
            GizmoScreenSize = gizmoScreenSize;
        }

        public Color RestBox { get; } = new(1f, 1f, 1f, 0.35f);

        /// <summary>Cage of the building under the mouse that a click would select.</summary>
        public Color Preview { get; } = new(1f, 1f, 1f, 0.45f);

        /// <summary>Invisible circle drawn every frame so the overlay renders the cage cylinders.</summary>
        public Color KeepAlive { get; } = new(1f, 1f, 1f, 0.02f); // not 0, in case empty colours get skipped
        public float KeepAliveDiameter { get; } = 0.05f;
        public Color HoveredHandle { get; } = new(0.45f, 0.95f, 1f, 1f);
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

        /// <summary>Hovered and selected edges are drawn this much thicker.</summary>
        public float HighlightRadiusFactor { get; } = 2f;

        /// <summary>Dot of a hovered corner or face.</summary>
        public float HandleDiameter { get; }

        /// <summary>Dot of the selected corner or face.</summary>
        public float SelectedHandleDiameter { get; }

        /// <summary>Shortest gizmo arrow in metres, used when the camera is close.</summary>
        public float AxisLength { get; }

        /// <summary>Gizmo arrow length as a part of the view height.</summary>
        public float GizmoScreenSize { get; }

        /// <summary>Arrow thickness relative to its length, so the gizmo keeps its look at any zoom.</summary>
        public float GizmoRadiusRatio { get; } = 0.03f;
        public float ActiveAxisRadiusFactor { get; } = 1.8f;

        public float GizmoLength(float cameraDistance, ViewScale scale) =>
            GizmoAxes.AxisLength(cameraDistance, scale.AngularSizeOfViewFraction(GizmoScreenSize), AxisLength);

        public Color Axis(int axis) => axis switch
        {
            0 => AxisX,
            1 => AxisY,
            _ => AxisZ,
        };

        /// <summary>Hovered or dragged arrows are lifted towards white.</summary>
        public Color ActiveAxis(int axis) => Color.Lerp(Axis(axis), Color.white, 0.45f);

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
