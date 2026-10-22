using System;
using System.Reflection;
using Colossal.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Input;
using Game.Prefabs;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Unsquare.UI.Overlay;

namespace Unsquare.UI.Tool
{
    /// <summary>
    /// Unsquare mode: a game tool that owns the mouse. A first click picks the building, a second one a handle of
    /// the pick mode; dragging a gizmo arrow moves it along that axis, dragging the gizmo square or the handle
    /// itself moves it horizontally; right-click steps back. Only talks to the session.
    /// </summary>
    public partial class UnsquareToolSystem : ToolBaseSystem
    {
        private const int MaxOwnerDepth = 8;

        // The game's tool Cancel also takes Escape, which would then never reach the game's Back and pause menu.
        private static readonly FieldInfo MouseCancelField =
            typeof(ToolBaseSystem).GetField("m_MouseCancel", BindingFlags.Instance | BindingFlags.NonPublic);

        private CageEditorSystem _editor;
        private IProxyAction _mouseCancel;
        private CageOverlaySystem _overlay;
        private IHandleDrag _drag;

        // A press on a handle drags it horizontally once the mouse moved past the threshold; until then it's a click.
        private PlaneDrag _pendingDrag;
        private CageHandle _pendingHandle;
        private float3 _pressDirection;
        private bool _loggedFailure;

        public override string toolID => "Unsquare";

        public bool IsActive => m_ToolSystem.activeTool == this;

        protected override void OnCreate()
        {
            base.OnCreate();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
            _overlay = World.GetOrCreateSystemManaged<CageOverlaySystem>();
            _mouseCancel = TryGetMouseCancel();
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
            else
                Mod.Log.Info($"[Edit] Unsquare mode is not available in {m_ToolSystem.actionMode}.");
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            // Like the default tool, also hit lots: parks are mostly flat ground without a mesh to click.
            m_ToolRaycastSystem.typeMask = TypeMask.StaticObjects | TypeMask.Areas;
            m_ToolRaycastSystem.areaTypeMask = AreaTypeMask.Lots;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.BuildingLots;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            cancelActionOverride = _mouseCancel; // the base resets it when the tool stops
            cancelAction.shouldBeEnabled = true;
            ResetDrags();
            _loggedFailure = false;
            var session = _editor.Session;
            session.SetEnabled(true);
            if (TryResolveBuilding(m_ToolSystem.selected, out var building))
                SelectBuilding(session, building);
        }

        private IProxyAction TryGetMouseCancel()
        {
            try
            {
                if (MouseCancelField?.GetValue(this) is IProxyAction action)
                    return action;
                Mod.Log.Warn("Mouse cancel action not found; Esc steps back in Unsquare mode.");
            }
            catch (Exception e)
            {
                Mod.Log.Warn(e, "Mouse cancel action not readable; Esc steps back in Unsquare mode.");
            }
            return null;
        }

        protected override void OnStopRunning()
        {
            ResetDrags();
            _editor.Session.SetEnabled(false);
            base.OnStopRunning();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            applyMode = ApplyMode.Clear;
            var session = _editor.Session;
            try
            {
                if (_drag != null)
                    UpdateDrag(session);
                else if (_pendingDrag != null)
                    UpdatePendingDrag(session);
                else
                    UpdatePointer(session);
            }
            catch (Exception e)
            {
                ResetDrags();
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
                session.SetPreview(Entity.Null);
                return;
            }

            bool hasContext = TryGetPickContext(session, out var context);
            float along = 0f;
            var hover = hasContext ? FindHover(session, context, out along) : CageHover.None;
            session.SetHover(hover);

            // Only an unlocked editor can switch buildings, so only then is the building under the mouse previewed.
            var hovered = Entity.Null;
            if (!session.IsLocked && GetRaycastResult(out Entity hit, out RaycastHit _))
                TryResolveBuilding(hit, out hovered);
            session.SetPreview(hover.HasHandle || hover.HasAxis ? Entity.Null : hovered);

            // Right-click steps back but never leaves the mode; only the toggle does.
            if (cancelAction.WasPressedThisFrame())
            {
                session.StepBack();
                return;
            }
            if (!applyAction.WasPressedThisFrame())
                return;

            // Clicks near the cage never change the selection; far clicks step back (handle, then building).
            // A selected handle locks the building: other buildings cannot be picked until it is released.
            if (hover.HasAxis)
                StartDrag(session, context, hover.Axis);
            else if (hover.HasHandle)
                PressHandle(session, context, hover.Handle, along);
            else if (session.IsLocked)
            {
                if (hasContext && !IsNearCage(session, context))
                    session.ClearSelection();
            }
            else if (hovered != Entity.Null && hovered != session.Target)
                SelectBuilding(session, hovered);
            else if (hovered == Entity.Null && hasContext && !IsNearCage(session, context))
                session.ClearTarget();
        }

        private static bool IsNearCage(CageEditSession session, in PickContext context) =>
            CageProximity.IsNear(context.Cage.Box, context.Cage.TargetOffsets, context.Ray,
                session.Options.FarClickMarginFraction);

        // The gizmo only exists with a selected handle (arrows win over the square); cage parts only of the
        // current pick mode. along: ray distance to the grabbed spot on a handle.
        private static CageHover FindHover(CageEditSession session, in PickContext context, out float along)
        {
            along = 0f;
            if (context.HasGizmo)
            {
                if (GizmoAxes.TryPickAxis(context.GizmoOrigin, context.GizmoLength, context.Ray, context.Tolerance,
                        out int axis))
                    return CageHover.OnAxis(axis);
                if (GizmoAxes.TryPickPlane(context.GizmoOrigin, context.GizmoLength, context.Ray))
                    return CageHover.OnAxis(GizmoAxes.PlaneXZ);
            }
            var cage = context.Cage;
            return CagePicker.TryPick(cage.Box, cage.TargetOffsets, context.Ray, context.Tolerance,
                session.Selection.PickKind, out var handle, out along)
                ? CageHover.OnHandle(handle)
                : CageHover.None;
        }

        private void StartDrag(CageEditSession session, in PickContext context, int axis)
        {
            if (!context.HasGizmo)
                return;
            IHandleDrag drag = null;
            if (axis == GizmoAxes.PlaneXZ)
            {
                if (PlaneDrag.TryStart(context.GizmoOrigin.y, context.Ray, out var planeDrag))
                    drag = planeDrag;
            }
            else if (AxisDrag.TryStart(context.GizmoOrigin, axis, context.Ray, out var axisDrag))
                drag = axisDrag;
            if (drag != null && session.BeginDrag(axis))
                _drag = drag;
        }

        // Selects the handle and arms a horizontal drag through the grabbed spot, so the spot stays under the mouse.
        private void PressHandle(CageEditSession session, in PickContext context, CageHandle handle, float along)
        {
            session.SelectHandle(handle);
            if (!session.Selection.HasHandle || session.Selection.Handle != handle ||
                !PlaneDrag.TryStart(context.Ray.PointAt(along).y, context.Ray, out var drag))
                return;
            _pendingDrag = drag;
            _pendingHandle = handle;
            _pressDirection = context.Ray.Direction;
        }

        private void UpdatePendingDrag(CageEditSession session)
        {
            if (cancelAction.WasPressedThisFrame() || !applyAction.IsPressed() || !session.Selection.HasHandle ||
                session.Selection.Handle != _pendingHandle)
            {
                _pendingDrag = null; // a click: the handle just stays selected
                return;
            }
            if (!TryGetPickContext(session, out var context) ||
                !DragThreshold.IsExceeded(_pressDirection, context.Ray.Direction, context.DragThreshold))
                return;
            var drag = _pendingDrag;
            _pendingDrag = null;
            if (!session.BeginDrag(GizmoAxes.PlaneXZ))
                return;
            _drag = drag;
            if (drag.TryDelta(context.Ray, out var delta))
                session.UpdateDrag(delta);
        }

        private void UpdateDrag(CageEditSession session)
        {
            if (cancelAction.WasPressedThisFrame())
            {
                _drag = null;
                session.CancelDrag();
                return;
            }
            if (session.ActiveDrag == null)
            {
                _drag = null; // target vanished or mode left
                return;
            }
            if (!applyAction.IsPressed())
            {
                _drag = null;
                session.EndDrag();
                return;
            }
            if (TryGetPickContext(session, out var context) && _drag.TryDelta(context.Ray, out var delta))
                session.UpdateDrag(delta);
        }

        private void ResetDrags()
        {
            _drag = null;
            _pendingDrag = null;
        }

        private static void SelectBuilding(CageEditSession session, Entity building)
        {
            if (session.SelectBuilding(building) && !session.TryGetCage(out _))
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

            float tolerance = view.Scale.AngularSizeOfPixels(session.Options.PickRadiusPixels);
            float dragThreshold = view.Scale.AngularSizeOfPixels(session.Options.DragThresholdPixels);
            var selection = session.Selection;
            if (!selection.HasHandle)
            {
                context = new PickContext(cage, ray, tolerance, dragThreshold, false, float3.zero, 0f);
                return true;
            }
            float3 origin = cage.HandlePosition(selection.Handle);
            float distance = math.distance(view.Position, BuildingSpace.PointToWorld(transform, origin));
            context = new PickContext(cage, ray, tolerance, dragThreshold, true, origin,
                _overlay.Style.GizmoLength(distance, view.Scale));
            return true;
        }

        /// <summary>Everything needed to pick in building space for one frame.</summary>
        private readonly struct PickContext
        {
            public PickContext(CageState cage, CageRay ray, float tolerance, float dragThreshold, bool hasGizmo,
                float3 gizmoOrigin, float gizmoLength)
            {
                Cage = cage;
                Ray = ray;
                Tolerance = tolerance;
                DragThreshold = dragThreshold;
                HasGizmo = hasGizmo;
                GizmoOrigin = gizmoOrigin;
                GizmoLength = gizmoLength;
            }

            public CageState Cage { get; }
            public CageRay Ray { get; }
            public float Tolerance { get; }

            /// <summary>Angle the mouse ray must turn after a press on a handle before it drags.</summary>
            public float DragThreshold { get; }
            public bool HasGizmo { get; }
            public float3 GizmoOrigin { get; }
            public float GizmoLength { get; }
        }
    }
}
