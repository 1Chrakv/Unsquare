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
        private CageState _dragStart;

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

        /// <summary>
        /// Raised when mode, target, selection, hover, step, align mode, drag or the target's cage state changed.
        /// </summary>
        public event Action Changed;

        /// <summary>Raised when <see cref="EditMode"/> changed, e.g. to remember it in the settings.</summary>
        public event Action<CageTarget> EditModeChanged;

        /// <summary>Unsquare mode is on; set by the tool. While off, every command does nothing.</summary>
        public bool IsEnabled { get; private set; }

        public Entity Target { get; private set; } = Entity.Null;
        public bool HasTarget => Target != Entity.Null;

        /// <summary>Building under the mouse that a click would select; its cage is previewed. Never the target.</summary>
        public Entity PreviewBuilding { get; private set; } = Entity.Null;

        /// <summary>Which cage edits move: the building, the lot or both (default, as before the split).</summary>
        public CageTarget EditMode { get; private set; } = CageTarget.Both;

        /// <summary>Pick mode and selected handle; a selected handle shows the gizmo.</summary>
        public HandleSelection Selection { get; private set; } = HandleSelection.Default;

        /// <summary>While a handle is selected, clicks cannot switch to another building (step back first).</summary>
        public bool IsLocked => Selection.HasHandle;

        /// <summary>Nudge sizes and pick radius; set via <see cref="Configure"/>.</summary>
        public CageEditOptions Options { get; private set; } = CageEditOptions.Default;
        public NudgeStep Step { get; private set; } = NudgeStep.Default;

        /// <summary>How <see cref="AlignToNeighbour"/> moves the face; kept while the game runs.</summary>
        public AlignMode AlignMode { get; private set; } = AlignMode.Angle;

        /// <summary>Which corner columns <see cref="FitToGround"/> moves; kept while the game runs.</summary>
        public GroundFitMode GroundFitMode { get; private set; } = GroundFitMode.BackOnly;

        /// <summary>Whether the gizmo moves or rotates the selected handle; kept while the game runs.</summary>
        public GizmoMode GizmoMode { get; private set; } = GizmoMode.Move;

        /// <summary>The gizmo shows rotation rings: Rotate mode and a handle that can turn (corners keep arrows).</summary>
        public bool ShowsRings =>
            GizmoMode == GizmoMode.Rotate && Selection.HasHandle && GizmoAxes.HasRings(Selection.Handle);

        public CageDrag ActiveDrag { get; private set; }

        /// <summary>Gizmo part (<see cref="GizmoAxes"/>: axis, square or ring) of the active drag, or -1.</summary>
        public int DragAxis { get; private set; } = -1;

        /// <summary>What the mouse is over; set by the mouse tool, read by views.</summary>
        public CageHover Hover { get; private set; } = CageHover.None;

        public DeformRequestResult? LastResult { get; private set; }

        /// <summary>Why the latest command failed; cleared by the next one that succeeds and by a new target.</summary>
        public EditProblem? Problem { get; private set; }

        /// <summary>Counts failures, so views can tell a repeated failure from the one they already showed.</summary>
        public int ProblemCount { get; private set; }

        public bool TryGetCage(out CageState state)
        {
            state = default;
            return HasTarget && _service.TryGetCage(Target, EditMode, out state);
        }

        public bool TryGetPreviewCage(out CageState state)
        {
            state = default;
            return PreviewBuilding != Entity.Null && _service.TryGetCage(PreviewBuilding, EditMode, out state);
        }

        /// <summary>Switches which cage edits move; a drag in progress is cancelled, the selection stays.</summary>
        public void SetEditMode(CageTarget mode)
        {
            if (mode == EditMode)
                return;
            CancelDrag();
            EditMode = mode;
            Log($"Edit mode {mode}");
            EditModeChanged?.Invoke(mode);
            Changed?.Invoke();
        }

        /// <summary>Both → Building → Lot → Both.</summary>
        public void CycleEditMode()
        {
            if (!IsEnabled)
                return;
            SetEditMode(EditMode switch
            {
                CageTarget.Both => CageTarget.Building,
                CageTarget.Building => CageTarget.Lot,
                _ => CageTarget.Both,
            });
        }

        /// <summary>Makes the building follow the lot again, dropping the building's own shape; undoable.</summary>
        public DeformRequestResult LinkBuildingToLot() => Link(CageTarget.Building, "Link building to lot");

        /// <summary>Makes the lot follow the building again, giving it the building's shape; undoable.</summary>
        public DeformRequestResult LinkLotToBuilding() => Link(CageTarget.Lot, "Link lot to building");

        private DeformRequestResult Link(CageTarget follower, string command)
        {
            if (!IsEnabled || !TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            var result = _service.Link(Target, follower);
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        /// <summary>Sets what follows the building's walls on the ground, also ahead of a split; undoable.</summary>
        public DeformRequestResult SetBlendMode(BlendMode mode)
        {
            string command = $"Blend {mode}";
            if (!IsEnabled || !TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            if (mode == state.BlendMode)
                return DeformRequestResult.Accepted;
            var result = _service.SetBlendMode(Target, mode);
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        /// <summary>Switches between blending everything and blending nothing.</summary>
        public DeformRequestResult ToggleBlendMode()
        {
            if (!TryGetCage(out var state))
                return Record("Blend", DeformRequestResult.NotDeformable);
            return SetBlendMode(state.BlendMode == BlendMode.All ? BlendMode.None : BlendMode.All);
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

        /// <summary>Starts dragging the selected handle by a gizmo part (axis, square or ring).</summary>
        public bool BeginDrag(int part)
        {
            if (!Selection.HasHandle || !TryGetCage(out var state))
                return false;
            ActiveDrag = new CageDrag(Selection.Handle, state.Box, state.TargetOffsets);
            _dragStart = state;
            DragAxis = part;
            Changed?.Invoke();
            return true;
        }

        /// <param name="motion">Move or turn since <see cref="BeginDrag"/>, in building space.</param>
        /// <returns>The service's answer, or the last one while the offsets stay on the same grid step.</returns>
        public DeformRequestResult UpdateDrag(HandleMotion motion)
        {
            if (ActiveDrag == null)
                return DeformRequestResult.NotDeformable;
            if (!ActiveDrag.TryAdvance(motion, out var offsets))
                return LastResult ?? DeformRequestResult.Accepted;
            return Record(null, _service.SetOffsets(Target, _dragStart, offsets));
        }

        public void EndDrag()
        {
            if (ActiveDrag == null)
                return;
            var handle = ActiveDrag.Handle;
            if (TryGetCage(out var state))
                _history.Record(Target, _dragStart.TargetSpec, state.TargetSpec);
            ActiveDrag = null;
            DragAxis = -1;
            Log($"Drag of {handle} ended -> {LastResult?.ToString() ?? "unchanged"}");
            Changed?.Invoke();
        }

        /// <summary>Restores the cage from drag start (the start view applied as an edit, so it lands exactly).</summary>
        public void CancelDrag()
        {
            if (ActiveDrag == null)
                return;
            var drag = ActiveDrag;
            ActiveDrag = null;
            DragAxis = -1;
            Record($"Drag of {drag.Handle} cancelled",
                _service.SetOffsets(Target, _dragStart, _dragStart.TargetOffsets));
        }

        public DeformRequestResult Reset()
        {
            if (!TryGetCage(out var state))
                return Record("Reset", DeformRequestResult.NotDeformable);
            var result = _service.Reset(Target, EditMode);
            RememberEdit(state.TargetSpec, result);
            return Record($"Reset ({EditMode})", result);
        }

        /// <summary>
        /// Puts the target's bottom corners on the original terrain (<see cref="GroundFitMode"/>), applied a frame or
        /// two later; in Building mode seats the building on its lot's ground instead.
        /// </summary>
        public DeformRequestResult FitToGround()
        {
            var mode = GroundFitMode;
            string command = EditMode == CageTarget.Building ? "Seat building on lot ground"
                : mode == GroundFitMode.BackOnly ? "Fit to ground (back)" : "Fit to ground (all corners)";
            if (!TryGetCage(out _))
                return Record(command, DeformRequestResult.NotDeformable);
            var building = Target;
            bool started = _service.TryFitToGround(building, EditMode, mode, (before, result) =>
            {
                RememberEdit(building, before, result);
                Record($"{command} on {building}", result);
            });
            if (!started)
                return Record(command, DeformRequestResult.TerrainUnavailable);
            if (EditMode == CageTarget.Building)
                return LastResult ?? DeformRequestResult.Accepted; // seated at once, already recorded
            Log($"{command}: sampling terrain");
            return DeformRequestResult.Accepted;
        }

        /// <summary>Back only ↔ all corners: whether fitting moves the street side to the ground too.</summary>
        public void ToggleGroundFitMode() => SetGroundFitMode(
            GroundFitMode == GroundFitMode.BackOnly ? GroundFitMode.AllCorners : GroundFitMode.BackOnly);

        public void SetGroundFitMode(GroundFitMode mode)
        {
            if (!IsEnabled || mode == GroundFitMode)
                return;
            if (EditMode == CageTarget.Building)
            {
                Fail("Ground fit mode applies to the lot; seating the building uses all corners",
                    EditProblem.GroundFitOfLot);
                return;
            }
            GroundFitMode = mode;
            Log($"Ground fit mode {GroundFitMode}");
            Changed?.Invoke();
        }

        /// <summary>Switches whether the target's lot terrain follows the cage; only for deformed buildings.</summary>
        public DeformRequestResult ToggleTerrainLift()
        {
            if (!TryGetCage(out var state))
                return Record("Terrain lift", DeformRequestResult.NotDeformable);
            if (EditMode == CageTarget.Building)
            {
                Fail("Terrain lift belongs to the lot", EditProblem.TerrainOfLot);
                return DeformRequestResult.Rejected;
            }
            bool lift = !state.TerrainLift;
            var result = _service.SetTerrainLift(Target, lift);
            RememberEdit(state.TargetSpec, result);
            return Record($"Terrain lift {(lift ? "on" : "off")}", result);
        }

        /// <summary>Aligns the target's left, back or right lot face to its nearest neighbour (<see cref="AlignMode"/>).</summary>
        public DeformRequestResult AlignToNeighbour(AlignSide side)
        {
            string command = $"Align {side} to neighbour ({AlignMode}, {EditMode})";
            if (!TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            var result = _service.AlignToNeighbour(Target, EditMode, side, AlignMode, Options.Align);
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        /// <summary>Moves the bottom of the target's left, back or right side to its neighbour's ground height.</summary>
        public DeformRequestResult AlignGroundToNeighbour(AlignSide side)
        {
            string command = $"Level {side} with neighbour ({EditMode})";
            if (!TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            var result = _service.AlignGroundToNeighbour(Target, EditMode, side, Options.Align);
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        /// <summary>Move ↔ Rotate.</summary>
        public void ToggleGizmoMode() =>
            SetGizmoMode(GizmoMode == GizmoMode.Move ? GizmoMode.Rotate : GizmoMode.Move);

        /// <summary>Switches the gizmo; refused mid-drag, so a drag always keeps the part it grabbed.</summary>
        public void SetGizmoMode(GizmoMode mode)
        {
            if (!IsEnabled || ActiveDrag != null || mode == GizmoMode)
                return;
            GizmoMode = mode;
            Hover = CageHover.None;
            Log($"Gizmo mode {GizmoMode}");
            Changed?.Invoke();
        }

        /// <summary>Angle → Flush → Full → Angle.</summary>
        public void CycleAlignMode() => SetAlignMode(AlignMode switch
        {
            AlignMode.Angle => AlignMode.Flush,
            AlignMode.Flush => AlignMode.Full,
            _ => AlignMode.Angle,
        });

        public void SetAlignMode(AlignMode mode)
        {
            if (!IsEnabled || mode == AlignMode)
                return;
            AlignMode = mode;
            Log($"Align mode {AlignMode}");
            Changed?.Invoke();
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

        /// <summary>
        /// Converts the city's FlexIt buildings. Runs outside Unsquare mode (options page); not undoable, so it also
        /// clears the history.
        /// </summary>
        public FlexItImportResult ImportFlexIt()
        {
            _history.Clear();
            var result = _service.ImportFlexIt();
            Log($"FlexIt import: {result}");
            return result;
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
            return $"Building {Target}: {state.Status}, {EditMode} cage on {state.Box}, {Selection}, step {Step}, " +
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
            Problem = null;
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
                if (!_service.TryGetCage(edit.Building, EditMode, out _))
                {
                    _history.Forget(edit.Building);
                    continue;
                }
                SetTarget(edit.Building);
                Record($"{(undo ? "Undo" : "Redo")} on {edit.Building}",
                    _service.Apply(edit.Building, undo ? edit.Before : edit.After));
                return;
            }
            if (undo)
                Fail("Nothing to undo", EditProblem.NothingToUndo);
            else
                Fail("Nothing to redo", EditProblem.NothingToRedo);
        }

        // Undoable change of the target's offsets that needs a selected handle.
        private DeformRequestResult EditHandle(string command, Func<CageOffsets, CageOffsets> change)
        {
            if (HasTarget && !Selection.HasHandle)
            {
                Fail("No handle selected", EditProblem.NoHandle);
                return DeformRequestResult.NotDeformable;
            }
            if (!TryGetCage(out var state))
                return Record(command, DeformRequestResult.NotDeformable);
            var result = _service.SetOffsets(Target, state, change(state.TargetOffsets));
            RememberEdit(state.TargetSpec, result);
            return Record(command, result);
        }

        private void RememberEdit(DeformSpec before, DeformRequestResult result) =>
            RememberEdit(Target, before, result);

        // The request is read back, so clamping is part of the edit.
        private void RememberEdit(Entity building, DeformSpec before, DeformRequestResult result)
        {
            if ((result is DeformRequestResult.Accepted or DeformRequestResult.Clamped) &&
                _service.TryGetCage(building, EditMode, out var state))
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

        // Drag frames pass no command: they are not logged, but a refused frame still reports its problem.
        private DeformRequestResult Record(string command, DeformRequestResult result)
        {
            LastResult = result;
            var problem = ProblemOf(result);
            if (command != null && problem == null)
                Log($"{command} -> {result}");
            else if (command != null)
                Mod.Log.Warn($"[Edit] {command} -> {result}");
            Problem = problem;
            if (problem != null)
                ProblemCount++;
            WarnIfLeavingLot();
            Changed?.Invoke();
            return result;
        }

        // A failure the session finds itself, before asking the service.
        private void Fail(string message, EditProblem problem)
        {
            Mod.Log.Warn($"[Edit] {message}");
            Problem = problem;
            ProblemCount++;
            Changed?.Invoke();
        }

        private static EditProblem? ProblemOf(DeformRequestResult result) => result switch
        {
            DeformRequestResult.Accepted or DeformRequestResult.Clamped => null,
            DeformRequestResult.Rejected => EditProblem.CageFolds,
            DeformRequestResult.BlendFolds => EditProblem.BlendFolds,
            DeformRequestResult.NoNeighbour => EditProblem.NoNeighbour,
            DeformRequestResult.TerrainUnavailable => EditProblem.TerrainUnavailable,
            DeformRequestResult.AlreadyLinked => EditProblem.AlreadyLinked,
            DeformRequestResult.LotUndeformed => EditProblem.LotUndeformed,
            _ => EditProblem.NotDeformable,
        };

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
