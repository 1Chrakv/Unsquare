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
    /// Holds target building, cage scope, selected handle, step and drag; turns commands into service requests.
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

        /// <summary>Raised when target, cursor, step, drag or the target's cage state changed.</summary>
        public event Action Changed;

        public Entity Target { get; private set; } = Entity.Null;
        public bool HasTarget => Target != Entity.Null;

        /// <summary>Scope for buildings that are not deformed yet; objects without a lot fall back to Building.</summary>
        public const CageScope DefaultScope = CageScope.Lot;

        /// <summary>Which cage is edited: the building's mesh bounds or its whole lot.</summary>
        public CageScope Scope { get; private set; } = DefaultScope;
        public CageHandleCursor Cursor { get; private set; }
        public NudgeStep Step { get; private set; } = NudgeStep.Default;
        public CageDrag ActiveDrag { get; private set; }
        public DeformRequestResult? LastResult { get; private set; }

        public bool TryGetCage(out CageState state)
        {
            state = default;
            return HasTarget && _service.TryGetCage(Target, Scope, out state);
        }

        public void SetTarget(Entity building)
        {
            if (building == Target)
                return;
            ActiveDrag = null; // a drag never carries over to another building
            Target = building;
            LastResult = null;
            // Deformed buildings open in their own scope; undeformed ones in the default (lot) scope.
            Scope = DefaultScope;
            if (HasTarget && _service.TryGetCage(Target, null, out var state) &&
                !(state.AppliedOffsets.IsIdentity && state.TargetOffsets.IsIdentity))
                Scope = state.Scope;
            Changed?.Invoke();
        }

        /// <summary>Switches between building and lot cage; the next edit replaces a deformation in the other scope.</summary>
        public void ToggleScope()
        {
            if (!HasTarget)
                return;
            ActiveDrag = null;
            var next = Scope == CageScope.Building ? CageScope.Lot : CageScope.Building;
            if (_service.TryGetCage(Target, next, out var state) && state.Scope != next)
            {
                Log($"{next} cage not available for building {Target}");
                return;
            }
            bool deformedElsewhere = _service.TryGetCage(Target, Scope, out var current) &&
                                     !current.AppliedOffsets.IsIdentity;
            Scope = next;
            Log(deformedElsewhere
                ? $"Scope {Scope}: the next edit replaces the current deformation"
                : $"Scope {Scope}");
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
                HasTarget
                    ? _service.MoveHandle(Target, Scope, Cursor.Handle, direction * Step.Meters)
                    : DeformRequestResult.NotDeformable);

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
                : Record(null, _service.SetOffsets(Target, Scope, ActiveDrag.OffsetsFor(totalDelta)));

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
            Record($"Drag of {Cursor} cancelled", _service.SetOffsets(Target, Scope, start));
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
            return $"Building {Target}: {state.Status}, {state.Scope} cage, handle {Cursor}, step {Step}, box {state.Box}, " +
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
