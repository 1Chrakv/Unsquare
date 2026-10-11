using System;
using Colossal.Entities;
using Game;
using Game.Buildings;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Unsquare.Api;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Unsquare.UI.Overlay;

namespace Unsquare.UI.Tool
{
    /// <summary>
    /// Unsquare mode: a game tool that owns the mouse. Clicks pick the target building and its edges and faces
    /// (corners with Ctrl); dragging a gizmo arrow moves the selected handle. Only talks to the session.
    /// </summary>
    public partial class UnsquareToolSystem : ToolBaseSystem
    {
        private const int MaxOwnerDepth = 8;

        private CageEditorSystem _editor;
        private CageOverlaySystem _overlay;
        private AxisDrag _axisDrag;
        private bool _loggedFailure;

        public override string toolID => "Unsquare";

        public bool IsActive => m_ToolSystem.activeTool == this;

        protected override void OnCreate()
        {
            base.OnCreate();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
            _overlay = World.GetOrCreateSystemManaged<CageOverlaySystem>();
        }

        public override PrefabBase GetPrefab() => null;

        public override bool TrySetPrefab(PrefabBase prefab) => false;

        /// <summary>Switches Unsquare mode on or off (back to the default tool).</summary>
        public void Toggle()
        {
            if (IsActive)
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            else if (m_ToolSystem.actionMode.IsGameOrEditor())
                m_ToolSystem.activeTool = this;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.StaticObjects;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            _axisDrag = null;
            _loggedFailure = false;
            var session = _editor.Session;
            session.SetEnabled(true);
            if (TryResolveBuilding(m_ToolSystem.selected, out var building))
                SetTarget(session, building);
        }

        protected override void OnStopRunning()
        {
            _axisDrag = null;
            _editor.Session.SetEnabled(false);
            base.OnStopRunning();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            applyMode = ApplyMode.Clear;
            var session = _editor.Session;
            try
            {
                if (_axisDrag != null)
                    UpdateDrag(session);
                else
                    UpdatePointer(session);
            }
            catch (Exception e)
            {
                _axisDrag = null;
                session.CancelDrag();
                if (!_loggedFailure)
                    Mod.Log.Error(e, "Unsquare tool update failed");
                _loggedFailure = true;
            }
            return inputDeps;
        }

        private void UpdatePointer(CageEditSession session)
        {
            bool overUi = (m_ToolRaycastSystem.raycastFlags & (RaycastFlags.DebugDisable | RaycastFlags.UIDisable)) != 0;
            if (overUi)
            {
                session.SetHover(CageHover.None);
                return;
            }

            bool corners = IsCtrlPressed();
            bool hasContext = TryGetPickContext(session, out var context);
            var hover = hasContext ? FindHover(session, context, corners) : CageHover.None;
            session.SetHover(hover);

            if (cancelAction.WasPressedThisFrame())
            {
                if (session.HasTarget)
                    session.ClearTarget();
                else
                    Toggle();
                return;
            }
            if (!applyAction.WasPressedThisFrame())
                return;

            if (hover.HasAxis)
                StartDrag(session, context, hover.Axis);
            else if (hover.HasHandle)
                session.SelectHandle(hover.Handle);
            else if (!corners)
                SetTarget(session, GetRaycastResult(out Entity hit, out RaycastHit _) &&
                                   TryResolveBuilding(hit, out var building)
                    ? building
                    : Entity.Null);
        }

        // Ctrl only changes what is picked; it never starts a drag, so Ctrl+click on an arrow selects a corner.
        private static CageHover FindHover(CageEditSession session, in PickContext context, bool corners)
        {
            var cage = context.Cage;
            if (corners)
                return CagePicker.TryPickCorner(cage.Box, cage.TargetOffsets, context.Ray, context.Tolerance,
                    out var corner)
                    ? CageHover.OnHandle(corner)
                    : CageHover.None;
            if (GizmoAxes.TryPickAxis(context.GizmoOrigin, context.GizmoLength, context.Ray, context.Tolerance,
                    out int axis))
                return CageHover.OnAxis(axis);
            return CagePicker.TryPick(cage.Box, cage.TargetOffsets, context.Ray, context.Tolerance, false,
                out var handle)
                ? CageHover.OnHandle(handle)
                : CageHover.None;
        }

        private void StartDrag(CageEditSession session, in PickContext context, int axis)
        {
            if (AxisDrag.TryStart(context.GizmoOrigin, axis, context.Ray, out var drag) && session.BeginDrag(axis))
                _axisDrag = drag;
        }

        private void UpdateDrag(CageEditSession session)
        {
            if (cancelAction.WasPressedThisFrame())
            {
                _axisDrag = null;
                session.CancelDrag();
                return;
            }
            if (session.ActiveDrag == null)
            {
                _axisDrag = null; // target vanished or mode left
                return;
            }
            if (!applyAction.IsPressed())
            {
                _axisDrag = null;
                session.EndDrag();
                return;
            }
            if (TryGetPickContext(session, out var context) && _axisDrag.TryDelta(context.Ray, out var delta))
                session.UpdateDrag(delta);
        }

        private void SetTarget(CageEditSession session, Entity building)
        {
            session.SetTarget(building);
            if (building != Entity.Null && !session.TryGetCage(out _))
                Mod.Log.Info($"[Edit] Building {building} has no lot cage and cannot be deformed.");
        }

        // Clicks on props, extensions or lot parts select the building that owns them.
        private bool TryResolveBuilding(Entity entity, out Entity building)
        {
            for (int depth = 0; depth < MaxOwnerDepth && entity != Entity.Null && EntityManager.Exists(entity); depth++)
            {
                if (EntityManager.HasComponent<Building>(entity))
                {
                    building = entity;
                    return true;
                }
                if (!EntityManager.TryGetComponent(entity, out Owner owner))
                    break;
                entity = owner.m_Owner;
            }
            building = Entity.Null;
            return false;
        }

        private bool TryGetPickContext(CageEditSession session, out PickContext context)
        {
            context = default;
            if (!session.TryGetCage(out var cage) ||
                !BuildingSpace.TryGetTransform(EntityManager, session.Target, out var transform) ||
                !CameraView.TryCapture(out var view))
                return false;

            var line = ToolRaycastSystem.CalculateRaycastLine(view.Camera);
            float3 direction = BuildingSpace.DirectionToBuilding(transform, line.b - line.a);
            if (!(math.lengthsq(direction) > 1e-8f))
                return false;
            var ray = new CageRay(BuildingSpace.PointToBuilding(transform, line.a), direction);

            float3 origin = cage.HandlePosition(session.Cursor.Handle);
            float distance = math.distance(view.Position, BuildingSpace.PointToWorld(transform, origin));
            context = new PickContext(cage, ray, view.Scale.AngularSizeOfPixels(session.Options.PickRadiusPixels),
                origin, _overlay.Style.GizmoLength(distance, view.Scale));
            return true;
        }

        private static bool IsCtrlPressed()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.ctrlKey.isPressed;
        }

        /// <summary>Everything needed to pick in building space for one frame.</summary>
        private readonly struct PickContext
        {
            public PickContext(CageState cage, CageRay ray, float tolerance, float3 gizmoOrigin, float gizmoLength)
            {
                Cage = cage;
                Ray = ray;
                Tolerance = tolerance;
                GizmoOrigin = gizmoOrigin;
                GizmoLength = gizmoLength;
            }

            public CageState Cage { get; }
            public CageRay Ray { get; }
            public float Tolerance { get; }
            public float3 GizmoOrigin { get; }
            public float GizmoLength { get; }
        }
    }
}
