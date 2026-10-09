using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// The mod's options page. For now it only holds the cage editing key bindings (all rebindable);
    /// general options will join it as the UI grows.
    /// </summary>
    [FileLocation(nameof(Unsquare))]
    [SettingsUIGroupOrder(HotkeyGroup)]
    [SettingsUIShowGroupName(HotkeyGroup)]
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
    [SettingsUIKeyboardAction(CageHotkeys.LogState, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    [SettingsUIKeyboardAction(CageHotkeys.ToggleScope, ActionType.Button, Usages.kDefaultUsage, Usages.kToolUsage)]
    public sealed class UnsquareSettings : ModSetting
    {
        public const string MainTab = "Main";
        public const string HotkeyGroup = "Hotkeys";

        public UnsquareSettings(IMod mod) : base(mod)
        {
        }

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
        [SettingsUIKeyboardBinding(BindingKeyboard.L, CageHotkeys.LogState, alt: true)]
        public ProxyBinding LogStateBinding { get; set; }

        [SettingsUISection(MainTab, HotkeyGroup)]
        [SettingsUIKeyboardBinding(BindingKeyboard.T, CageHotkeys.ToggleScope, alt: true)]
        public ProxyBinding ToggleScopeBinding { get; set; }

        public override void SetDefaults()
        {
            // Key bindings take their defaults from the attributes above.
        }
    }
}
