using Colossal.UI.Binding;
using Game.UI;
using Unsquare.UI.Tool;

namespace Unsquare.UI.Bindings
{
    /// <summary>
    /// Bindings for the UI module (UiModule/): whether Unsquare mode is on, and the toggle behind the
    /// toolbar button. Group and names must match unsquare-toggle.tsx.
    /// </summary>
    public partial class UnsquareUISystem : UISystemBase
    {
        private const string Group = "Unsquare";

        private UnsquareToolSystem _tool;

        protected override void OnCreate()
        {
            base.OnCreate();
            _tool = World.GetOrCreateSystemManaged<UnsquareToolSystem>();
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "modeActive", () => _tool.IsActive));
            AddBinding(new TriggerBinding(Group, "toggleMode", ToggleMode));
        }

        // Logged so a button that seems dead shows whether the click reaches the mod.
        private void ToggleMode()
        {
            Mod.Log.Info($"[UI] Toggle requested (active={_tool.IsActive})");
            _tool.Toggle();
        }
    }
}
