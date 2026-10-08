using System.Collections.Generic;
using Colossal.Mathematics;
using Game;
using Game.Rendering;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Transform = Game.Objects.Transform;

namespace Unsquare.UI.Overlay
{
    /// <summary>
    /// Read-only view of the edit session: draws rest box, deformed cage (coloured by status),
    /// the handles of the current kind and axes on the selected handle around the target building.
    /// </summary>
    public partial class CageOverlaySystem : GameSystemBase
    {
        private readonly CageSegment[] _edges = new CageSegment[CageTopology.EdgeCount];
        private readonly List<CageSegment> _dashes = new();
        private readonly CageOverlayStyle _style = CageOverlayStyle.Default;
        private CageEditorSystem _editor;
        private OverlayRenderSystem _overlayRenderSystem;

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
            DrawWireframe(buffer, transform, cage.Box, cage.TargetOffsets, _style.Cage(cage.Status), dashed: false);
            DrawHandles(buffer, transform, cage, session.Cursor);
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
                    DrawLine(buffer, transform, edge, color);
                    continue;
                }
                // The built-in dashed line is a flat ribbon that vanishes when vertical; dash ourselves instead.
                _dashes.Clear();
                SegmentDashes.Split(edge, _style.DashLength, _style.GapLength, _dashes);
                foreach (var dash in _dashes)
                    DrawLine(buffer, transform, dash, color);
            }
        }

        // Camera-facing: flat ribbons have no visible width on vertical segments.
        private void DrawLine(OverlayRenderSystem.Buffer buffer, in Transform transform, CageSegment local, Color color)
        {
            var segment = new Line3.Segment(
                BuildingSpace.PointToWorld(transform, local.A), BuildingSpace.PointToWorld(transform, local.B));
            buffer.DrawLine(color, segment, _style.LineWidth, true);
        }

        private void DrawHandles(OverlayRenderSystem.Buffer buffer, in Transform transform, in CageState cage,
            CageHandleCursor cursor)
        {
            int count = CageTopology.CountOf(cursor.Kind);
            for (int i = 0; i < count; i++)
            {
                if (i == cursor.Index)
                    continue;
                var handle = new CageHandleCursor(cursor.Kind, i).Handle;
                buffer.DrawCircle(_style.Handle, BuildingSpace.HandleWorldPosition(transform, cage, handle),
                    _style.HandleDiameter);
            }

            float3 local = cage.HandlePosition(cursor.Handle);
            float3 world = BuildingSpace.PointToWorld(transform, local);
            buffer.DrawCircle(_style.SelectedHandle, world, _style.SelectedHandleDiameter);
            DrawAxis(buffer, transform, local, new float3(1, 0, 0), _style.AxisX);
            DrawAxis(buffer, transform, local, new float3(0, 1, 0), _style.AxisY);
            DrawAxis(buffer, transform, local, new float3(0, 0, 1), _style.AxisZ);
        }

        // Axes are drawn in building space so they show exactly what the move hotkeys do.
        private void DrawAxis(OverlayRenderSystem.Buffer buffer, in Transform transform, float3 origin, float3 axis,
            Color color) =>
            DrawLine(buffer, transform, new CageSegment(origin, origin + axis * _style.AxisLength), color);
    }
}
