using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// The mod's options page: cage editing key bindings (all rebindable) on the main tab, tunables on the
    /// advanced tab (see UnsquareSettings.Advanced.cs).
    /// </summary>
    [FileLocation(nameof(Unsquare))]
    [SettingsUITabOrder(MainTab, AdvancedTab)]
    [SettingsUIGroupOrder(HotkeyGroup, EditingGroup, LimitsGroup, AttachmentsGroup, LotSyncGroup, OverlayGroup,
        PerformanceGroup, FlexItGroup, ResetGroup)]
    [SettingsUIShowGroupName(HotkeyGroup, EditingGroup, LimitsGroup, AttachmentsGroup, LotSyncGroup, OverlayGroup,
        PerformanceGroup, FlexItGroup)]
    [SettingsUIKeyboardAction(CageHotkeys.ToggleMode, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ExitMode, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.CyclePickKind, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.KindCorner, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.KindEdge, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.KindFace, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.PreviousHandle, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.NextHandle, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MoveNegX, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MovePosX, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MovePosZ, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MoveNegZ, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MovePosY, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.MoveNegY, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.CycleStep, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ResetHandle, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ResetSelected, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ToggleTerrainLift, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.FitToGround, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ToggleGroundFitMode, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignLeft, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignBack, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignRight, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignGroundLeft, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignGroundBack, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.AlignGroundRight, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.CycleAlignMode, ActionType.Button, Usages.kDefaultUsage,
        Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ResetAll, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.Undo, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.Redo, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.LogState, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    public sealed partial class UnsquareSettings : ModSetting
    {
        public const string MainTab = "Main";
        public const string HotkeyGroup = "Hotkeys";

        public UnsquareSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.U, CageHotkeys.ToggleMode, alt: true)]
        public ProxyBinding ToggleModeBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Escape, CageHotkeys.ExitMode)]
        public ProxyBinding ExitModeBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.W, CageHotkeys.CyclePickKind, alt: true)]
        public ProxyBinding CyclePickKindBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit1, CageHotkeys.KindCorner, alt: true)]
        public ProxyBinding KindCornerBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit2, CageHotkeys.KindEdge, alt: true)]
        public ProxyBinding KindEdgeBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Digit3, CageHotkeys.KindFace, alt: true)]
        public ProxyBinding KindFaceBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Q, CageHotkeys.PreviousHandle, alt: true)]
        public ProxyBinding PreviousHandleBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.E, CageHotkeys.NextHandle, alt: true)]
        public ProxyBinding NextHandleBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, CageHotkeys.MoveNegX, alt: true)]
        public ProxyBinding MoveNegXBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, CageHotkeys.MovePosX, alt: true)]
        public ProxyBinding MovePosXBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, CageHotkeys.MovePosZ, alt: true)]
        public ProxyBinding MovePosZBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.DownArrow, CageHotkeys.MoveNegZ, alt: true)]
        public ProxyBinding MoveNegZBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.PageUp, CageHotkeys.MovePosY, alt: true)]
        public ProxyBinding MovePosYBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.PageDown, CageHotkeys.MoveNegY, alt: true)]
        public ProxyBinding MoveNegYBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.S, CageHotkeys.CycleStep, alt: true)]
        public ProxyBinding CycleStepBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.R, CageHotkeys.ResetHandle, alt: true)]
        public ProxyBinding ResetHandleBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Backspace, CageHotkeys.ResetSelected, alt: true)]
        public ProxyBinding ResetSelectedBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.T, CageHotkeys.ToggleTerrainLift, alt: true)]
        public ProxyBinding ToggleTerrainLiftBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.G, CageHotkeys.FitToGround, alt: true)]
        public ProxyBinding FitToGroundBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.F, CageHotkeys.ToggleGroundFitMode, alt: true)]
        public ProxyBinding ToggleGroundFitModeBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, CageHotkeys.AlignLeft, alt: true, shift: true)]
        public ProxyBinding AlignLeftBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, CageHotkeys.AlignBack, alt: true, shift: true)]
        public ProxyBinding AlignBackBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, CageHotkeys.AlignRight, alt: true, shift: true)]
        public ProxyBinding AlignRightBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.A, CageHotkeys.CycleAlignMode, alt: true)]
        public ProxyBinding CycleAlignModeBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.LeftArrow, CageHotkeys.AlignGroundLeft, alt: true, ctrl: true)]
        public ProxyBinding AlignGroundLeftBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.UpArrow, CageHotkeys.AlignGroundBack, alt: true, ctrl: true)]
        public ProxyBinding AlignGroundBackBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.RightArrow, CageHotkeys.AlignGroundRight, alt: true, ctrl: true)]
        public ProxyBinding AlignGroundRightBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Delete, CageHotkeys.ResetAll, alt: true, shift: true)]
        public ProxyBinding ResetAllBinding { get; set; }

        // Bindings are physical key positions (US layout): this is Ctrl+Z / Ctrl+Y on a German QWERTZ keyboard.
        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Y, CageHotkeys.Undo, ctrl: true)]
        public ProxyBinding UndoBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, CageHotkeys.Redo, ctrl: true)]
        public ProxyBinding RedoBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.L, CageHotkeys.LogState, alt: true)]
        public ProxyBinding LogStateBinding { get; set; }

        public override void SetDefaults()
        {
            // Key bindings take their defaults from the attributes above.
            SetAdvancedDefaults();
        }
    }
}
