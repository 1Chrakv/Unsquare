using System;
using Colossal.UI.Binding;
using Game.UI;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.Core.Terrain;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;

namespace Unsquare.UI.Bindings
{
    /// <summary>
    /// Bindings for the UI module (UiModule/): the mode toggle behind the toolbar button and the floating tool
    /// window above the edited building. Group and names must match unsquare-toggle.tsx and tool-window.tsx.
    /// </summary>
    public partial class UnsquareUISystem : UISystemBase
    {
        private const string Group = "Unsquare";

        // Keeps the window's anchor this far from the screen border (share of the screen size).
        private const float ScreenMargin = 0.04f;

        private UnsquareToolSystem _tool;
        private CageEditorSystem _editor;
        private bool _loggedAnchorFailure;

        protected override void OnCreate()
        {
            base.OnCreate();
            _tool = World.GetOrCreateSystemManaged<UnsquareToolSystem>();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "modeActive", () => _tool.IsActive));
            AddBinding(new TriggerBinding(Group, "toggleMode", () => _tool.Toggle()));

            AddUpdateBinding(new GetterValueBinding<ToolWindowAnchor>(Group, "toolWindow", GetToolWindowAnchor,
                new ValueWriter<ToolWindowAnchor>()));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "pickKind",
                () => _editor.Session.Selection.PickKind.ToString()));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "hasHandle",
                () => _editor.Session.Selection.HasHandle));
            AddBinding(new TriggerBinding<string>(Group, "selectPickKind", SelectPickKind));
            AddBinding(new TriggerBinding(Group, "resetHandle", () => _editor.Session.ResetHandle()));
            AddBinding(new TriggerBinding(Group, "resetBuilding", () => _editor.Session.Reset()));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "terrainLift",
                () => _editor.Session.TryGetCage(out var cage) && cage.TerrainLift));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "canLiftTerrain",
                () => _editor.Session.TryGetCage(out var cage) && cage.IsDeformed));
            AddBinding(new TriggerBinding(Group, "toggleTerrainLift", () => _editor.Session.ToggleTerrainLift()));
            AddBinding(new TriggerBinding(Group, "fitToGround", () => _editor.Session.FitToGround()));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "groundFitMode",
                () => _editor.Session.GroundFitMode.ToString()));
            AddBinding(new TriggerBinding<string>(Group, "selectGroundFitMode", SelectGroundFitMode));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "alignMode",
                () => _editor.Session.AlignMode.ToString()));
            AddBinding(new TriggerBinding<string>(Group, "selectAlignMode", SelectAlignMode));
            AddBinding(new TriggerBinding<string>(Group, "alignToNeighbour", AlignToNeighbour));
        }

        private void SelectGroundFitMode(string mode)
        {
            if (Enum.TryParse(mode, out GroundFitMode parsed) && Enum.IsDefined(typeof(GroundFitMode), parsed))
                _editor.Session.SetGroundFitMode(parsed);
            else
                Mod.Log.Warn($"[UI] Unknown ground fit mode '{mode}'.");
        }

        private void SelectAlignMode(string mode)
        {
            if (Enum.TryParse(mode, out AlignMode parsed) && Enum.IsDefined(typeof(AlignMode), parsed))
                _editor.Session.SetAlignMode(parsed);
            else
                Mod.Log.Warn($"[UI] Unknown align mode '{mode}'.");
        }

        private void AlignToNeighbour(string side)
        {
            if (Enum.TryParse(side, out AlignSide parsed) && Enum.IsDefined(typeof(AlignSide), parsed))
                _editor.Session.AlignToNeighbour(parsed);
            else
                Mod.Log.Warn($"[UI] Unknown align side '{side}'.");
        }

        private void SelectPickKind(string kind)
        {
            if (Enum.TryParse(kind, out CageHandleKind parsed) && Enum.IsDefined(typeof(CageHandleKind), parsed))
                _editor.Session.SelectKind(parsed);
            else
                Mod.Log.Warn($"[UI] Unknown pick kind '{kind}'.");
        }

        // Above the roof of the edited building; hidden without a deformable target or behind the camera.
        private ToolWindowAnchor GetToolWindowAnchor()
        {
            var session = _editor.Session;
            if (!_tool.IsActive || !session.TryGetCage(out var cage))
                return ToolWindowAnchor.Hidden;
            try
            {
                if (!BuildingSpace.TryGetTransform(EntityManager, session.Target, out var transform) ||
                    !CameraView.TryCapture(out var view))
                    return ToolWindowAnchor.Hidden;
                float3 world = BuildingSpace.PointToWorld(transform, CageAnchor.Top(cage.Box, cage.TargetOffsets));
                float3 viewport = view.Camera.WorldToViewportPoint(world);
                return ToolWindowPlacement.TryPlace(viewport, ScreenMargin, out var position)
                    ? new ToolWindowAnchor(position.x, position.y)
                    : ToolWindowAnchor.Hidden;
            }
            catch (Exception e)
            {
                if (!_loggedAnchorFailure)
                    Mod.Log.Error(e, "Placing the Unsquare tool window failed");
                _loggedAnchorFailure = true;
                return ToolWindowAnchor.Hidden;
            }
        }
    }
}
