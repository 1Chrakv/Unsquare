using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// UI-agnostic edit model shared by every input (hotkeys, mouse tool, later panels) and every view. Holds
    /// target, selected and hovered handle, step and drag; commands only run while Unsquare mode is enabled.
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

        /// <summary>Raised when mode, target, cursor, hover, step, drag or the target's cage state changed.</summary>
        public event Action Changed;

        /// <summary>Unsquare mode is on; set by the tool. While off, every command does nothing.</summary>
        public bool IsEnabled { get; private set; }

        public Entity Target { get; private set; } = Entity.Null;
        public bool HasTarget => Target != Entity.Null;

        /// <summary>Nudge sizes; set via <see cref="Configure"/>.</summary>
        public CageEditOptions Options { get; private set; } = CageEditOptions.Default;
        public CageHandleCursor Cursor { get; private set; }
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

        /// <summary>Turns Unsquare mode on or off; turning it off cancels a drag and clears the target.</summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled == IsEnabled)
                return;
            if (!enabled)
            {
                CancelDrag();
                ClearTarget();
            }
            IsEnabled = enabled;
            Log(enabled ? "Unsquare mode on" : "Unsquare mode off");
            Changed?.Invoke();
        }

        public void SetTarget(Entity building)
        {
            if (building == Target || (!IsEnabled && building != Entity.Null))
                return;
            ActiveDrag = null; // a drag never carries over to another building
            DragAxis = -1;
            Hover = CageHover.None;
            Target = building;
            LastResult = null;
            Changed?.Invoke();
        }

        public void ClearTarget() => SetTarget(Entity.Null);

        public void SelectKind(CageHandleKind kind) => SetCursor(Cursor.WithKind(kind));
        public void NextHandle() => SetCursor(Cursor.Next());
        public void PreviousHandle() => SetCursor(Cursor.Previous());
        public void SelectHandle(CageHandle handle) => SetCursor(CageHandleCursor.From(handle));

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
        public DeformRequestResult Nudge(float3 direction) =>
            Record($"Nudge {Cursor} {direction * Step.Meters}",
                HasTarget
                    ? _service.MoveHandle(Target, Cursor.Handle, direction * Step.Meters)
                    : DeformRequestResult.NotDeformable);

        /// <summary>Starts dragging the selected handle along a gizmo axis (0..2).</summary>
        public bool BeginDrag(int axis)
        {
            if (!TryGetCage(out var state))
                return false;
            ActiveDrag = new CageDrag(Cursor.Handle, state.TargetOffsets);
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
            ActiveDrag = null;
            DragAxis = -1;
            Log($"Drag of {Cursor} ended -> {LastResult?.ToString() ?? "unchanged"}");
            Changed?.Invoke();
        }

        /// <summary>Restores the offsets from drag start.</summary>
        public void CancelDrag()
        {
            if (ActiveDrag == null)
                return;
            var start = ActiveDrag.StartOffsets;
            ActiveDrag = null;
            DragAxis = -1;
            Record($"Drag of {Cursor} cancelled", _service.SetOffsets(Target, start));
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

        /// <summary>One-line summary of target, cursor, step and cage state, e.g. for logs.</summary>
        public string Describe()
        {
            if (!TryGetCage(out var state))
                return HasTarget ? $"Building {Target}: not deformable" : "No building selected";
            return $"Building {Target}: {state.Status}, handle {Cursor}, step {Step}, lot box {state.Box}, " +
                   $"applied [{state.AppliedOffsets}], target [{state.TargetOffsets}]";
        }

        // The selection never changes mid-drag (e.g. Ctrl+click), so a drag always moves what it grabbed.
        private void SetCursor(CageHandleCursor cursor)
        {
            if (!IsEnabled || ActiveDrag != null || cursor.Equals(Cursor))
                return;
            Cursor = cursor;
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
