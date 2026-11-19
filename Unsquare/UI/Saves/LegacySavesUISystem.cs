using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using Unsquare.Api;
using Unsquare.UI.Input;

namespace Unsquare.UI.Saves
{
    /// <summary>
    /// Bindings of the old-saves popup (legacy-saves-dialog.tsx): after a city loads with deformed buildings in the
    /// format of saves from before 1.2, it asks to resave old saves and offers to retire the legacy manifest.
    /// Group and names must match the dialog.
    /// </summary>
    public partial class LegacySavesUISystem : UISystemBase
    {
        private const string Group = "Unsquare";

        private UnsquareSettings _settings;
        private IBuildingDeformService _service;
        private bool _open;
        private int _count;
        private string _result = string.Empty;
        private bool _done;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "legacySavesPending", () => _open));
            AddUpdateBinding(new GetterValueBinding<int>(Group, "legacySavesCount", () => _count));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "legacySavesResult", () => _result));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "legacySavesDone", () => _done));
            AddBinding(new TriggerBinding(Group, "closeLegacySaves", () => _open = false));
            AddBinding(new TriggerBinding(Group, "hideLegacySaves", Hide));
            AddBinding(new TriggerBinding(Group, "retireLegacySaves", Retire));
        }

        protected override void OnDestroy()
        {
            if (_service != null)
                _service.LegacySaveLoaded -= OnLegacySaveLoaded;
            base.OnDestroy();
        }

        /// <summary>Hooks up the settings (the "don't show again" flag) and the service that reports old saves.</summary>
        internal void Bind(UnsquareSettings settings, IBuildingDeformService service)
        {
            _settings = settings;
            if (_service != null)
                _service.LegacySaveLoaded -= OnLegacySaveLoaded;
            _service = service;
            _service.LegacySaveLoaded += OnLegacySaveLoaded;
        }

        protected override void OnGamePreload(Purpose purpose, GameMode mode)
        {
            base.OnGamePreload(purpose, mode);
            _open = false; // never carried into the next load
        }

        private void OnLegacySaveLoaded(int count)
        {
            if (_settings != null && _settings.HideLegacySavesPrompt)
                return;
            _count = count;
            _result = string.Empty;
            _done = false;
            _open = true;
        }

        private void Hide()
        {
            _open = false;
            if (_settings == null)
                return;
            _settings.HideLegacySavesPrompt = true;
            _settings.ApplyAndSave();
        }

        private void Retire()
        {
            if (_service == null)
                return;
            var result = _service.RetireLegacyManifest();
            _done = result.Done;
            _result = result.ToString();
        }
    }
}
