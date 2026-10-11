using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// UI-agnostic edit model shared by every input (mouse tool, hotkeys, later panels) and every view. Holds target,
    /// pick mode, selected and hovered handle, step and drag; commands only run while Unsquare mode is enabled.
    /// </summary>
    public sealed class CageEditSession
    {
        private readonly IBuildingDeformService _service;
        private bool _warnedLeavingLot;

        public CageEditSession(IBuildingDeformService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
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

        /// <summary>Applies new options; the step keeps its position (small, medium, large).</summary>
        public void Configure(CageEditOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Step = Step.WithSizes(options.SmallStep, options.MediumStep, options.LargeStep);
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
            if (HasTarget && !Selection.HasHandle)
            {
                Log("No handle selected");
                return DeformRequestResult.NotDeformable;
            }
            return Record($"Nudge {Selection.Handle} {direction * Step.Meters}",
                HasTarget
                    ? _service.MoveHandle(Target, Selection.Handle, direction * Step.Meters)
                    : DeformRequestResult.NotDeformable);
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

        public DeformRequestResult Reset() =>
            Record("Reset", HasTarget ? _service.Reset(Target) : DeformRequestResult.NotDeformable);

        public int ResetAll()
        {
            if (!IsEnabled)
                return 0;
            int count = _service.ResetAll();
            Log($"Reset all: {count} buildings");
            return count;
        }

        /// <summary>One-line summary of target, selection, step and cage state, e.g. for logs.</summary>
        public string Describe()
        {
            if (!TryGetCage(out var state))
                return HasTarget ? $"Building {Target}: not deformable" : "No building selected";
            return $"Building {Target}: {state.Status}, {Selection}, step {Step}, lot box {state.Box}, " +
                   $"applied [{state.AppliedOffsets}], target [{state.TargetOffsets}]";
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
            Target = building;
            LastResult = null;
            Log(building == Entity.Null ? "Target cleared" : $"Target {building}");
            Changed?.Invoke();
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
                Mod.Log.Warn("[Edit] Lot outline leaves its plot: zone cells and terrain stay rectangular, neighbours may overlap.");
            _warnedLeavingLot = exceeds;
        }

        private static void Log(string message) => Mod.Log.Info($"[Edit] {message}");
    }
}
