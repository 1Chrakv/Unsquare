using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.Core.Terrain;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// UI-agnostic edit model shared by every input (mouse tool, hotkeys, later panels) and every view. Holds target,
    /// pick mode, selected and hovered handle, step and drag; commands only run while Unsquare mode is enabled.
    /// </summary>
    public sealed class CageEditSession
    {
        private readonly IBuildingDeformService _service;
        private readonly EditHistory<Entity> _history;
        private bool _warnedLeavingLot;

        public CageEditSession(IBuildingDeformService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _history = new EditHistory<Entity>(Options.HistoryDepth);
            _service.CageChanged += building =>
            {
                if (building == Target)
                    Changed?.Invoke();
            };
        }

        /// <summary>Raised when mode, target, selection, hover, step, drag or the target's cage state changed.</summary>
        public event Action Changed;

        /// <summary>Unsquare mode is on; set by the tool. While off, every command does nothing.</summary>
        public bool IsEnabled { get; private set; }

        public Entity Target { get; private set; } = Entity.Null;
        public bool HasTarget => Target != Entity.Null;

        /// <summary>Building under the mouse that a click would select; its cage is previewed. Never the target.</summary>
        public Entity PreviewBuilding { get; private set; } = Entity.Null;

        /// <summary>Pick mode and selected handle; a selected handle shows the gizmo.</summary>
        public HandleSelection Selection { get; private set; } = HandleSelection.Default;

        /// <summary>While a handle is selected, clicks cannot switch to another building (step back first).</summary>
        public bool IsLocked => Selection.HasHandle;

        /// <summary>Nudge sizes and pick radius; set via <see cref="Configure"/>.</summary>
        public CageEditOptions Options { get; private set; } = CageEditOptions.Default;
        public NudgeStep Step { get; private set; } = NudgeStep.Default;
        public CageDrag ActiveDrag { get; private set; }

        /// <summary>Gizmo axis (0..2) of the active drag, or -1.</summary>
        public int DragAxis { get; private set; } = -1;

        /// <summary>What the mouse is over; set by the mouse tool, read by views.</summary>
        public CageHover Hover { get; private set; } = CageHover.None;

        public DeformRequestResult? LastResult { get; private set; }

        public bool TryGetCage(out CageState state)
        {
            state = default;
            return HasTarget && _service.TryGetCage(Target, out state);
        }

        public bool TryGetPreviewCage(out CageState state)
        {
            state = default;
            return PreviewBuilding != Entity.Null && _service.TryGetCage(PreviewBuilding, out state);
        }

        /// <summary>Applies new options; the step keeps its position (small, medium, large).</summary>
        public void Configure(CageEditOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Step = Step.WithSizes(options.SmallStep, options.MediumStep, options.LargeStep);
            _history.Capacity = options.HistoryDepth;
            Changed?.Invoke();
        }

        /// <summary>Turns Unsquare mode on or off; turning it off cancels a drag and forgets target and selection.</summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled == IsEnabled)
                return;
            if (!enabled)
            {
                CancelDrag();
                ClearTarget();
                Selection = HandleSelection.Default;
                PreviewBuilding = Entity.Null;
            }
            IsEnabled = enabled;
            Log(enabled ? "Unsquare mode on" : "Unsquare mode off");
            Changed?.Invoke();
        }

        /// <summary>Makes a clicked building the target; refused while a handle is selected.</summary>
        public bool SelectBuilding(Entity building)
        {
            if (!IsEnabled || IsLocked || building == Entity.Null)
                return false;
            SetTarget(building);
            return true;
        }

        /// <summary>Drops the target regardless of the lock, e.g. when the building no longer exists.</summary>
        public void ClearTarget() => SetTarget(Entity.Null);

        /// <summary>Undoes the innermost state: drag, then selection, then target.</summary>
        public StepBackResult StepBack()
        {
            if (ActiveDrag != null)
            {
                CancelDrag();
                return StepBackResult.DragCancelled;
            }
            if (Selection.HasHandle)
            {
                ClearSelection();
                return StepBackResult.SelectionCleared;
            }
            if (HasTarget)
            {
                ClearTarget();
                return StepBackResult.TargetCleared;
            }
            return StepBackResult.None;
        }

        /// <summary>Sets the pick mode (corner, edge, face); a selected handle stays selected.</summary>
        public void SelectKind(CageHandleKind kind) => SetPickKind(Selection.WithPickKind(kind));

        /// <summary>Edge → Face → Corner → Edge.</summary>
        public void CyclePickKind() => SetPickKind(Selection.CyclePickKind());

        public void NextHandle() => SetSelection(Selection.Next());
        public void PreviousHandle() => SetSelection(Selection.Previous());
        public void SelectHandle(CageHandle handle) => SetSelection(Selection.Select(handle));
        public void ClearSelection() => SetSelection(Selection.Clear());

        /// <summary>Sets the previewed building; the target itself and anything outside the mode are not previewed.</summary>
        public void SetPreview(Entity building)
        {
            if (!IsEnabled || building == Target)
                building = Entity.Null;
            if (building == PreviewBuilding)
                return;
            PreviewBuilding = building;
            Changed?.Invoke();
        }

        public void SetHover(CageHover hover)
        {
            if (hover.Equals(Hover))
                return;
            Hover = hover;
            Changed?.Invoke();
        }

        public void CycleStep()
        {
            if (!IsEnabled)
                return;
            Step = Step.Next();
            Log($"Step {Step}");
            Changed?.Invoke();
        }

        /// <summary>Moves the selected handle by one step along a building-space direction (e.g. float3(0, 1, 0)).</summary>
        public DeformRequestResult Nudge(float3 direction)
        {
            var handle = Selection.Handle;
            var delta = direction * Step.Meters;
            return EditHandle($"Nudge {handle} {delta}", offsets => offsets.Move(handle, delta));
        }

        /// <summary>Puts the corners of the selected handle back to their original place; the rest keeps its shape.</summary>
        public DeformRequestResult ResetHandle()
        {
            var handle = Selection.Handle;
            return EditHandle($"Reset {handle}", offsets => offsets.Reset(handle));
        }

        /// <summary>Starts dragging the selected handle along a gizmo axis (0..2).</summary>
        public bool BeginDrag(int axis)
        {
            if (!Selection.HasHandle || !TryGetCage(out var state))
                return false;
            ActiveDrag = new CageDrag(Selection.Handle, state.TargetOffsets);
            DragAxis = axis;
            Changed?.Invoke();
            return true;
        }

        /// <param name="totalDelta">Building-space delta since <see cref="BeginDrag"/>.</param>
        /// <returns>The service's answer, or the last one while the offsets stay on the same grid step.</returns>
        public DeformRequestResult UpdateDrag(float3 totalDelta)
        {
            if (ActiveDrag == null)
                return DeformRequestResult.NotDeformable;
            if (!ActiveDrag.TryAdvance(totalDelta, out var offsets))
                return LastResult ?? DeformRequestResult.Accepted;
            return Record(null, _service.SetOffsets(Target, offsets));
        }

        public void EndDrag()
        {
            if (ActiveDrag == null)
                return;
            var handle = ActiveDrag.Handle;
            if (TryGetCage(out var state))
                _history.Record(Target, state.TargetSpec.WithOffsets(ActiveDrag.StartOffsets), state.TargetSpec);
            ActiveDrag = null;
            DragAxis = -1;
            Log($"Drag of {handle} ended -> {LastResult?.ToString() ?? "unchanged"}");
            Changed?.Invoke();
        }

        /// <summary>Restores the offsets from drag start.</summary>
        public void CancelDrag()
        {
            if (ActiveDrag == null)
                return;
            var drag = ActiveDrag;
            ActiveDrag = null;
            DragAxis = -1;
            Record($"Drag of {drag.Handle} cancelled", _service.SetOffsets(Target, drag.StartOffsets));
        }

        public DeformRequestResult Reset()
        {
            if (!TryGetCage(out var state))
                return Record("Reset", DeformRequestResult.NotDeformable);
            var result = _service.Reset(Target);
            RememberEdit(state.TargetSpec, result);
            return Record("Reset", result);
        }

        /// <summary>Puts the target's bottom corners on the original terrain; applied a frame or two later.</summary>
        public DeformRequestResult FitToGround(GroundFitMode mode)
        {
            string command = mode == GroundFitMode.BackOnly ? "Fit to ground (back)" : "Fit to ground (all corners)";
            if (!TryGetCage(out _))
                return Record(command, DeformRequestResult.NotDeformable);
            var building = Target;
            bool started = _service.TryFitToGround(building, mode, (before, result) =>
            {
                RememberEdit(building, before, result);
                Record($"{command} on {building}", result);
            });
            if (!started)
                return Record(command, DeformRequestResult.Rejected);
            Log($"{command}: sampling terrain");
            return DeformRequestResult.Accepted;
        }

        /// <summary>Switches whether the target's lot terrain follows the cage; only for deformed buildings.</summary>
        public DeformRequestResult ToggleTerrainLift()
        {
            if (!TryGetCage(out var state))
                return Record("Terrain lift", DeformRequestResult.NotDeformable);
            bool lift = !state.TerrainLift;
            var result = _service.SetTerrainLift(Target, lift);
            RememberEdit(state.TargetSpec, result);
            return Record($"Terrain lift {(lift ? "on" : "off")}", result);
        }

        /// <summary>Not undoable (uninstall path), so it also clears the history.</summary>
        public int ResetAll()
        {
            if (!IsEnabled)
                return 0;
            _history.Clear();
            int count = _service.ResetAll();
            Log($"Reset all: {count} buildings");
            return count;
        }

        /// <summary>Reverts the newest cage edit on any building; that building becomes the target.</summary>
        public void Undo() => Revisit(true);

        /// <summary>Repeats the newest undone cage edit; that building becomes the target.</summary>
        public void Redo() => Revisit(false);

        /// <summary>Forgets all edits, e.g. when another city is loaded.</summary>
        public void ClearHistory() => _history.Clear();

        /// <summary>One-line summary of target, selection, step and cage state, e.g. for logs.</summary>
        public string Describe()
        {
            if (!TryGetCage(out var state))
                return HasTarget ? $"Building {Target}: not deformable" : "No building selected";
            return $"Building {Target}: {state.Status}, {Selection}, step {Step}, lot box {state.Box}, " +
                   $"applied [{state.AppliedSpec}], target [{state.TargetSpec}]";
        }

        // A new target starts without a selected handle; the pick mode stays.
        private void SetTarget(Entity building)
        {
            if (building == Target)
                return;
            ActiveDrag = null; // a drag never carries over to another building
            DragAxis = -1;
            Hover = CageHover.None;
            Selection = Selection.Clear();
            PreviewBuilding = Entity.Null;
            Target = building;
            LastResult = null;
            Log(building == Entity.Null ? "Target cleared" : $"Target {building}");
            Changed?.Invoke();
        }

        // Edits of buildings that are gone are dropped and the next one is tried.
        private void Revisit(bool undo)
        {
            if (!IsEnabled || ActiveDrag != null)
                return;
            CageEdit<Entity> edit;
            while (undo ? _history.TryUndo(out edit) : _history.TryRedo(out edit))
            {
                if (!_service.TryGetCage(edit.Building, out _))
                {
                    _history.Forget(edit.Building);
                    continue;
                }
                SetTarget(edit.Building);
                Record($"{(undo ? "Undo" : "Redo")} on {edit.Building}",
                    _service.Apply(edit.Building, undo ? edit.Before : edit.After));
                return;
            }
            Log(undo ? "Nothing to undo" : "Nothing to redo");
        }

        // Undoable change of the target's offsets that needs a selected handle.
        private DeformRequestResult EditHandle(string command, Func<CageOffsets, CageOffsets> change)
        {
            if (HasTarget && !Selection.HasHandle)
            {
                Log("No handle selected");
                return DeformRequestResult.NotDeformable;
            }
            if (!TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            var result = _service.SetOffsets(Target, change(state.TargetOffsets));
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        private void RememberEdit(DeformSpec before, DeformRequestResult result) =>
            RememberEdit(Target, before, result);

        // The request is read back, so clamping is part of the edit.
        private void RememberEdit(Entity building, DeformSpec before, DeformRequestResult result)
        {
            if ((result is DeformRequestResult.Accepted or DeformRequestResult.Clamped) &&
                _service.TryGetCage(building, out var state))
                _history.Record(building, before, state.TargetSpec);
        }

        private void SetPickKind(HandleSelection selection)
        {
            if (!IsEnabled || selection.Equals(Selection))
                return;
            Selection = selection;
            Log($"Pick mode {selection.PickKind}");
            Changed?.Invoke();
        }

        // The selection never changes mid-drag, so a drag always moves what it grabbed.
        private void SetSelection(HandleSelection selection)
        {
            if (!IsEnabled || !HasTarget || ActiveDrag != null || selection.Equals(Selection))
                return;
            Selection = selection;
            Log(selection.HasHandle ? $"Selected {selection.Handle}" : "Selection cleared");
            Changed?.Invoke();
        }

        private DeformRequestResult Record(string command, DeformRequestResult result)
        {
            LastResult = result;
            if (command != null)
                Log($"{command} -> {result}");
            WarnIfLeavingLot();
            Changed?.Invoke();
            return result;
        }

        // Logs once when the lot outline starts to leave its plot, not on every drag frame.
        private void WarnIfLeavingLot()
        {
            bool exceeds = TryGetCage(out var state) && state.ExceedsLot;
            if (exceeds && !_warnedLeavingLot)
                Mod.Log.Warn("[Edit] Lot outline leaves its plot: zone cells and the terrain outline stay rectangular, neighbours may overlap.");
            _warnedLeavingLot = exceeds;
        }

        private static void Log(string message) => Mod.Log.Info($"[Edit] {message}");
    }
}
