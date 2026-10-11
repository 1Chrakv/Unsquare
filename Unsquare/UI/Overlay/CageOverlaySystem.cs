using System.Collections.Generic;
using Game;
using Game.Rendering;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;
using Transform = Game.Objects.Transform;

namespace Unsquare.UI.Overlay
{
    /// <summary>
    /// Read-only view of the edit session: draws rest box, deformed cage (coloured by status), the hovered
    /// and selected handle and the move gizmo on the selected handle around the target building.
    /// </summary>
    public partial class CageOverlaySystem : GameSystemBase
    {
        private readonly CageSegment[] _edges = new CageSegment[CageTopology.EdgeCount];
        private readonly List<CageSegment> _dashes = new();
        private CageEditorSystem _editor;
        private OverlayRenderSystem _overlayRenderSystem;
        private CageOverlayStyle _style = CageOverlayStyle.Default;

        /// <summary>Colours and sizes; replaced when the options change.</summary>
        public CageOverlayStyle Style
        {
            get => _style;
            set => _style = value ?? throw new System.ArgumentNullException(nameof(value));
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
            _overlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
        }

        protected override void OnUpdate()
        {
            var session = _editor.Session;
            if (!session.TryGetCage(out var cage) ||
                !BuildingSpace.TryGetTransform(EntityManager, session.Target, out var transform))
                return;

            var buffer = _overlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete(); // we draw on the main thread
            DrawWireframe(buffer, transform, cage.Box, CageOffsets.Identity, _style.RestBox, dashed: true);
            var cageColor = cage.ExceedsLot ? _style.LotWarning : _style.Cage(cage.Status);
            DrawWireframe(buffer, transform, cage.Box, cage.TargetOffsets, cageColor, dashed: false);

            // The cage is always drawn while there is a target; selection and gizmo only with a selected handle.
            var selection = session.Selection;
            var hover = session.Hover;
            if (hover.HasHandle && (!selection.HasHandle || hover.Handle != selection.Handle))
                DrawHandle(buffer, transform, cage, hover.Handle, _style.HoveredHandle, _style.HandleDiameter);
            if (selection.HasHandle)
            {
                DrawHandle(buffer, transform, cage, selection.Handle, _style.SelectedHandle,
                    _style.SelectedHandleDiameter);
                int activeAxis = session.DragAxis >= 0 ? session.DragAxis : hover.Axis;
                if (CameraView.TryCapture(out var view))
                    DrawGizmo(buffer, transform, cage.HandlePosition(selection.Handle), view, activeAxis);
            }
            _overlayRenderSystem.AddBufferWriter(default);
        }

        private void DrawWireframe(OverlayRenderSystem.Buffer buffer, in Transform transform, CageBox box,
            CageOffsets offsets, Color color, bool dashed)
        {
            CageWireframe.Fill(box, offsets, _edges);
            foreach (var edge in _edges)
            {
                if (!dashed)
                {
                    DrawSegment(buffer, transform, edge, color, _style.LineRadius);
                    continue;
                }
                _dashes.Clear();
                SegmentDashes.Split(edge, _style.DashLength, _style.GapLength, _dashes);
                foreach (var dash in _dashes)
                    DrawSegment(buffer, transform, dash, color, _style.LineRadius);
            }
        }

        // Corners get a dot, edges a thick line, faces a thick outline and a dot in the centre.
        private void DrawHandle(OverlayRenderSystem.Buffer buffer, in Transform transform, in CageState cage,
            CageHandle handle, Color color, float dotDiameter)
        {
            var corners = CageTopology.CornersOf(handle);
            float radius = _style.LineRadius * _style.HighlightRadiusFactor;
            switch (handle.Kind)
            {
                case CageHandleKind.Edge:
                    DrawSegment(buffer, transform,
                        new CageSegment(cage.CornerPosition(corners[0]), cage.CornerPosition(corners[1])), color, radius);
                    return;
                case CageHandleKind.Face:
                    // Face corners come in bit order of the two in-plane axes; 0-1-3-2 walks around the quad.
                    DrawSegment(buffer, transform, FaceSide(cage, corners[0], corners[1]), color, radius);
                    DrawSegment(buffer, transform, FaceSide(cage, corners[1], corners[3]), color, radius);
                    DrawSegment(buffer, transform, FaceSide(cage, corners[3], corners[2]), color, radius);
                    DrawSegment(buffer, transform, FaceSide(cage, corners[2], corners[0]), color, radius);
                    break;
            }
            buffer.DrawCircle(color, BuildingSpace.HandleWorldPosition(transform, cage, handle), dotDiameter);
        }

        private static CageSegment FaceSide(in CageState cage, int a, int b) =>
            new(cage.CornerPosition(a), cage.CornerPosition(b));

        // Arrows along the building axes, i.e. exactly what a drag (and the move hotkeys) moves along.
        private void DrawGizmo(OverlayRenderSystem.Buffer buffer, in Transform transform, float3 origin,
            in CameraView view, int activeAxis)
        {
            float distance = math.distance(view.Position, BuildingSpace.PointToWorld(transform, origin));
            float length = _style.GizmoLength(distance, view.Scale);
            float radius = length * _style.GizmoRadiusRatio;
            for (int axis = 0; axis < GizmoAxes.AxisCount; axis++)
            {
                bool active = axis == activeAxis;
                var color = active ? _style.ActiveAxis(axis) : _style.Axis(axis);
                float axisRadius = active ? radius * _style.ActiveAxisRadiusFactor : radius;
                float3 direction = GizmoAxes.Direction(axis);
                float3 tip = origin + direction * length;
                DrawSegment(buffer, transform, new CageSegment(origin, tip), color, axisRadius);
                DrawSegment(buffer, transform, new CageSegment(tip - direction * (length * 0.2f), tip), color,
                    axisRadius * 2.2f);
            }
        }

        // Overlay lines measure length on the ground plane only, so vertical ones vanish; cylinders work at any slope.
        private void DrawSegment(OverlayRenderSystem.Buffer buffer, in Transform transform, CageSegment local,
            Color color, float radius)
        {
            float3 a = BuildingSpace.PointToWorld(transform, local.A);
            float3 b = BuildingSpace.PointToWorld(transform, local.B);
            if (!SegmentPose.TryCreate(a, b, out var pose))
                return;
            buffer.DrawCustomMesh(color, pose.Center, pose.Length, radius,
                OverlayRenderSystem.CustomMeshType.Cylinder, pose.Rotation);
        }
    }
}
