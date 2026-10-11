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
        PerformanceGroup, ResetGroup)]
    [SettingsUIShowGroupName(HotkeyGroup, EditingGroup, LimitsGroup, AttachmentsGroup, LotSyncGroup, OverlayGroup,
        PerformanceGroup)]
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
    [SettingsUIKeyboardAction(CageHotkeys.ResetSelected, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
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
        [SettingsUIKeyboardBinding(BindingKeyboard.Backspace, CageHotkeys.ResetSelected, alt: true)]
        public ProxyBinding ResetSelectedBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Delete, CageHotkeys.ResetAll, alt: true, shift: true)]
        public ProxyBinding ResetAllBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Z, CageHotkeys.Undo, ctrl: true)]
        public ProxyBinding UndoBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.Y, CageHotkeys.Redo, ctrl: true)]
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
