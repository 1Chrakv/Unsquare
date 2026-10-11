using System.Collections.Generic;
using Unity.Mathematics;
using Unsquare.Core.Cage;

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

        /// <summary>Leaves Unsquare mode (default Esc); ignored outside the mode.</summary>
        public const string ExitMode = "UnsquareExitMode";
        public const string ExitModeLabel = "Leave Unsquare mode";

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
        public const string ResetSelected = "UnsquareResetSelected";
        public const string ResetAll = "UnsquareResetAll";
        public const string LogState = "UnsquareLogState";

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
            new HotkeyDefinition(ResetSelected, nameof(UnsquareSettings.ResetSelectedBinding), "Reset selected building",
                s => s.Reset()),
            new HotkeyDefinition(ResetAll, nameof(UnsquareSettings.ResetAllBinding), "Reset all buildings",
                s => s.ResetAll()),
            new HotkeyDefinition(LogState, nameof(UnsquareSettings.LogStateBinding), "Write cage state to the log",
                s => Mod.Log.Info($"[Edit] {s.Describe()}")),
        };
    }
}
