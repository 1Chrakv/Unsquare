using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// Action names and the hotkey → session command table. Action names are stored in the user's
    /// key-binding settings, so keep them stable.
    /// </summary>
    internal static class CageHotkeys
    {
        /// <summary>Turns Unsquare mode on and off; the only action that works outside the mode.</summary>
        public const string ToggleMode = "UnsquareToggleMode";
        public const string ToggleModeLabel = "Toggle Unsquare mode";

        public const string CyclePickKind = "UnsquareCyclePickKind";
        public const string KindCorner = "UnsquareKindCorner";
        public const string KindEdge = "UnsquareKindEdge";
        public const string KindFace = "UnsquareKindFace";
        public const string PreviousHandle = "UnsquarePreviousHandle";
        public const string NextHandle = "UnsquareNextHandle";
        public const string MoveNegX = "UnsquareMoveNegX";
        public const string MovePosX = "UnsquareMovePosX";
        public const string MovePosZ = "UnsquareMovePosZ";
        public const string MoveNegZ = "UnsquareMoveNegZ";
        public const string MovePosY = "UnsquareMovePosY";
        public const string MoveNegY = "UnsquareMoveNegY";
        public const string CycleStep = "UnsquareCycleStep";
        public const string ResetHandle = "UnsquareResetHandle";
        public const string ResetSelected = "UnsquareResetSelected";
        public const string ToggleTerrainLift = "UnsquareToggleTerrainLift";
        public const string FitToGround = "UnsquareFitToGround";
        public const string ToggleGroundFitMode = "UnsquareToggleGroundFitMode";
        public const string AlignLeft = "UnsquareAlignLeft";
        public const string AlignBack = "UnsquareAlignBack";
        public const string AlignRight = "UnsquareAlignRight";
        public const string CycleAlignMode = "UnsquareCycleAlignMode";
        public const string AlignGroundLeft = "UnsquareAlignGroundLeft";
        public const string AlignGroundBack = "UnsquareAlignGroundBack";
        public const string AlignGroundRight = "UnsquareAlignGroundRight";
        public const string ResetAll = "UnsquareResetAll";
        public const string LogState = "UnsquareLogState";
        public const string Undo = "UnsquareUndo";
        public const string Redo = "UnsquareRedo";

        public static readonly IReadOnlyList<HotkeyDefinition> All = new[]
        {
            new HotkeyDefinition(CyclePickKind, nameof(UnsquareSettings.CyclePickKindBinding),
                "Cycle pick mode (edges, faces, corners)", s => s.CyclePickKind()),
            new HotkeyDefinition(KindCorner, nameof(UnsquareSettings.KindCornerBinding), "Pick corners",
                s => s.SelectKind(CageHandleKind.Corner)),
            new HotkeyDefinition(KindEdge, nameof(UnsquareSettings.KindEdgeBinding), "Pick edges",
                s => s.SelectKind(CageHandleKind.Edge)),
            new HotkeyDefinition(KindFace, nameof(UnsquareSettings.KindFaceBinding), "Pick faces",
                s => s.SelectKind(CageHandleKind.Face)),
            new HotkeyDefinition(PreviousHandle, nameof(UnsquareSettings.PreviousHandleBinding),
                "Select previous handle", s => s.PreviousHandle()),
            new HotkeyDefinition(NextHandle, nameof(UnsquareSettings.NextHandleBinding), "Select next handle",
                s => s.NextHandle()),
            new HotkeyDefinition(MoveNegX, nameof(UnsquareSettings.MoveNegXBinding), "Move handle -X (width)",
                s => s.Nudge(new float3(-1, 0, 0))),
            new HotkeyDefinition(MovePosX, nameof(UnsquareSettings.MovePosXBinding), "Move handle +X (width)",
                s => s.Nudge(new float3(1, 0, 0))),
            new HotkeyDefinition(MovePosZ, nameof(UnsquareSettings.MovePosZBinding), "Move handle +Z (towards street)",
                s => s.Nudge(new float3(0, 0, 1))),
            new HotkeyDefinition(MoveNegZ, nameof(UnsquareSettings.MoveNegZBinding), "Move handle -Z (away from street)",
                s => s.Nudge(new float3(0, 0, -1))),
            new HotkeyDefinition(MovePosY, nameof(UnsquareSettings.MovePosYBinding), "Move handle up",
                s => s.Nudge(new float3(0, 1, 0))),
            new HotkeyDefinition(MoveNegY, nameof(UnsquareSettings.MoveNegYBinding), "Move handle down",
                s => s.Nudge(new float3(0, -1, 0))),
            new HotkeyDefinition(CycleStep, nameof(UnsquareSettings.CycleStepBinding), "Cycle step size",
                s => s.CycleStep()),
            new HotkeyDefinition(ResetHandle, nameof(UnsquareSettings.ResetHandleBinding),
                "Reset selected corner, edge or face", s => s.ResetHandle()),
            new HotkeyDefinition(ResetSelected, nameof(UnsquareSettings.ResetSelectedBinding), "Reset selected building",
                s => s.Reset()),
            new HotkeyDefinition(ToggleTerrainLift, nameof(UnsquareSettings.ToggleTerrainLiftBinding),
                "Toggle terrain lift", s => s.ToggleTerrainLift()),
            new HotkeyDefinition(FitToGround, nameof(UnsquareSettings.FitToGroundBinding),
                "Fit to original ground (back only or all corners, see Alt+F)", s => s.FitToGround()),
            new HotkeyDefinition(ToggleGroundFitMode, nameof(UnsquareSettings.ToggleGroundFitModeBinding),
                "Toggle fitting the street side to the ground", s => s.ToggleGroundFitMode()),
            new HotkeyDefinition(AlignLeft, nameof(UnsquareSettings.AlignLeftBinding),
                "Align left side to neighbour (seen from the street)", s => s.AlignToNeighbour(AlignSide.Left)),
            new HotkeyDefinition(AlignBack, nameof(UnsquareSettings.AlignBackBinding), "Align back to neighbour",
                s => s.AlignToNeighbour(AlignSide.Back)),
            new HotkeyDefinition(AlignRight, nameof(UnsquareSettings.AlignRightBinding),
                "Align right side to neighbour (seen from the street)", s => s.AlignToNeighbour(AlignSide.Right)),
            new HotkeyDefinition(CycleAlignMode, nameof(UnsquareSettings.CycleAlignModeBinding),
                "Cycle align mode (angle, flush, full)", s => s.CycleAlignMode()),
            new HotkeyDefinition(AlignGroundLeft, nameof(UnsquareSettings.AlignGroundLeftBinding),
                "Level left side with neighbour (seen from the street)", s => s.AlignGroundToNeighbour(AlignSide.Left)),
            new HotkeyDefinition(AlignGroundBack, nameof(UnsquareSettings.AlignGroundBackBinding),
                "Level back with neighbour", s => s.AlignGroundToNeighbour(AlignSide.Back)),
            new HotkeyDefinition(AlignGroundRight, nameof(UnsquareSettings.AlignGroundRightBinding),
                "Level right side with neighbour (seen from the street)",
                s => s.AlignGroundToNeighbour(AlignSide.Right)),
            new HotkeyDefinition(ResetAll, nameof(UnsquareSettings.ResetAllBinding), "Reset all buildings",
                s => s.ResetAll()),
            new HotkeyDefinition(Undo, nameof(UnsquareSettings.UndoBinding), "Undo cage edit", s => s.Undo()),
            new HotkeyDefinition(Redo, nameof(UnsquareSettings.RedoBinding), "Redo cage edit", s => s.Redo()),
            new HotkeyDefinition(LogState, nameof(UnsquareSettings.LogStateBinding), "Write cage state to the log",
                s => Mod.Log.Info($"[Edit] {s.Describe()}")),
        };
    }
}
