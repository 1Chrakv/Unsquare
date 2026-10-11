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
using Unsquare.Api;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Unsquare.UI.Overlay;

namespace Unsquare.UI.Tool
{
    /// <summary>
    /// Unsquare mode: a game tool that owns the mouse. A first click picks the building, a second one a handle of
    /// the pick mode; dragging a gizmo arrow moves it; right-click steps back. Only talks to the session.
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
            else
                Mod.Log.Info($"[Edit] Unsquare mode is not available in {m_ToolSystem.actionMode}.");
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
                SelectBuilding(session, building);
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
                session.SetPreview(Entity.Null);
                return;
            }

            bool hasContext = TryGetPickContext(session, out var context);
            var hover = hasContext ? FindHover(session, context) : CageHover.None;
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
                session.SelectHandle(hover.Handle);
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

        // Arrows only exist with a selected handle; cage parts only of the current pick mode.
        private static CageHover FindHover(CageEditSession session, in PickContext context)
        {
            if (context.HasGizmo && GizmoAxes.TryPickAxis(context.GizmoOrigin, context.GizmoLength, context.Ray,
                    context.Tolerance, out int axis))
                return CageHover.OnAxis(axis);
            var cage = context.Cage;
            return CagePicker.TryPick(cage.Box, cage.TargetOffsets, context.Ray, context.Tolerance,
                session.Selection.PickKind, out var handle)
                ? CageHover.OnHandle(handle)
                : CageHover.None;
        }

        private void StartDrag(CageEditSession session, in PickContext context, int axis)
        {
            if (context.HasGizmo && AxisDrag.TryStart(context.GizmoOrigin, axis, context.Ray, out var drag) && session.BeginDrag(axis))
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
            var selection = session.Selection;
            if (!selection.HasHandle)
            {
                context = new PickContext(cage, ray, tolerance, false, float3.zero, 0f);
                return true;
            }
            float3 origin = cage.HandlePosition(selection.Handle);
            float distance = math.distance(view.Position, BuildingSpace.PointToWorld(transform, origin));
            context = new PickContext(cage, ray, tolerance, true, origin, _overlay.Style.GizmoLength(distance, view.Scale));
            return true;
        }

        /// <summary>Everything needed to pick in building space for one frame.</summary>
        private readonly struct PickContext
        {
            public PickContext(CageState cage, CageRay ray, float tolerance, bool hasGizmo, float3 gizmoOrigin,
                float gizmoLength)
            {
                Cage = cage;
                Ray = ray;
                Tolerance = tolerance;
                HasGizmo = hasGizmo;
                GizmoOrigin = gizmoOrigin;
                GizmoLength = gizmoLength;
            }

            public CageState Cage { get; }
            public CageRay Ray { get; }
            public float Tolerance { get; }
            public bool HasGizmo { get; }
            public float3 GizmoOrigin { get; }
            public float GizmoLength { get; }
        }
    }
}
