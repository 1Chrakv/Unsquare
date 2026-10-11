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
            AddBinding(new TriggerBinding(Group, "toggleMode", () => _tool.Toggle()));
        }
    }
}
