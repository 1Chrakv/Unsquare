using System;
using Unity.Entities;
using Unity.Mathematics;
using Unsquare.Api;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Editing
{
    /// <summary>
    /// UI-agnostic edit model shared by every input (hotkeys, later mouse drag and panels) and every view.
    /// Holds target building, selected handle, step and drag; translates commands into service requests.
    /// </summary>
    public sealed class CageEditSession
    {
        private readonly IBuildingDeformService _service;

        public CageEditSession(IBuildingDeformService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _service.CageChanged += building =>
            {
                if (building == Target)
                    Changed?.Invoke();
            };
        }

        /// <summary>Raised when target, cursor, step, drag or the target's cage state changed.</summary>
        public event Action Changed;

        public Entity Target { get; private set; } = Entity.Null;
        public bool HasTarget => Target != Entity.Null;
        public CageHandleCursor Cursor { get; private set; }
        public NudgeStep Step { get; private set; } = NudgeStep.Default;
        public CageDrag ActiveDrag { get; private set; }
        public DeformRequestResult? LastResult { get; private set; }

        public bool TryGetCage(out CageState state)
        {
            state = default;
            return HasTarget && _service.TryGetCage(Target, out state);
        }

        public void SetTarget(Entity building)
        {
            if (building == Target)
                return;
            ActiveDrag = null; // a drag never carries over to another building
            Target = building;
            LastResult = null;
            Changed?.Invoke();
        }

        public void SelectKind(CageHandleKind kind) => SetCursor(Cursor.WithKind(kind));
        public void NextHandle() => SetCursor(Cursor.Next());
        public void PreviousHandle() => SetCursor(Cursor.Previous());

        public void CycleStep()
        {
            Step = Step.Next();
            Log($"Step {Step}");
            Changed?.Invoke();
        }

        /// <summary>Moves the selected handle by one step along a building-space direction (e.g. float3(0, 1, 0)).</summary>
        public DeformRequestResult Nudge(float3 direction) =>
            Record($"Nudge {Cursor} {direction * Step.Meters}",
                HasTarget ? _service.MoveHandle(Target, Cursor.Handle, direction * Step.Meters) : DeformRequestResult.NotDeformable);

        public bool BeginDrag()
        {
            if (!TryGetCage(out var state))
                return false;
            ActiveDrag = new CageDrag(Cursor.Handle, state.TargetOffsets);
            Changed?.Invoke();
            return true;
        }

        /// <param name="totalDelta">Building-space delta since <see cref="BeginDrag"/>.</param>
        public DeformRequestResult UpdateDrag(float3 totalDelta) =>
            ActiveDrag == null
                ? DeformRequestResult.NotDeformable
                : Record(null, _service.SetOffsets(Target, ActiveDrag.OffsetsFor(totalDelta)));

        public void EndDrag()
        {
            if (ActiveDrag == null)
                return;
            ActiveDrag = null;
            Log($"Drag of {Cursor} ended");
            Changed?.Invoke();
        }

        /// <summary>Restores the offsets from drag start.</summary>
        public void CancelDrag()
        {
            if (ActiveDrag == null)
                return;
            var start = ActiveDrag.StartOffsets;
            ActiveDrag = null;
            Record($"Drag of {Cursor} cancelled", _service.SetOffsets(Target, start));
        }

        public DeformRequestResult Reset() =>
            Record("Reset", HasTarget ? _service.Reset(Target) : DeformRequestResult.NotDeformable);

        public int ResetAll()
        {
            int count = _service.ResetAll();
            Log($"Reset all: {count} buildings");
            return count;
        }

        /// <summary>One-line summary of target, cursor, step and cage state, e.g. for logs.</summary>
        public string Describe()
        {
            if (!TryGetCage(out var state))
                return HasTarget ? $"Building {Target}: not deformable" : "No building selected";
            return $"Building {Target}: {state.Status}, handle {Cursor}, step {Step}, box {state.Box}, " +
                   $"applied [{state.AppliedOffsets}], target [{state.TargetOffsets}]";
        }

        private void SetCursor(CageHandleCursor cursor)
        {
            if (cursor.Equals(Cursor))
                return;
            Cursor = cursor;
            Changed?.Invoke();
        }

        private DeformRequestResult Record(string command, DeformRequestResult result)
        {
            LastResult = result;
            if (command != null)
                Log($"{command} -> {result}");
            Changed?.Invoke();
            return result;
        }

        private static void Log(string message) => Mod.Log.Info($"[Edit] {message}");
    }
}
