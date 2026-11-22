using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.UI;
using Unsquare.Api;
using Unsquare.Core.Persistence;
using Unsquare.UI.Input;

namespace Unsquare.UI.Saves
{
    /// <summary>
    /// Bindings of the old-saves popup (legacy-saves-dialog.tsx) after loads tied to saves from before 1.2: it offers
    /// the retire (old-format or converted save), or restoring shapes the save misses because they were archived.
    /// Group and names must match the dialog.
    /// </summary>
    public partial class LegacySavesUISystem : UISystemBase
    {
        private const string Group = "Unsquare";
        private const string ConvertKind = "convert"; // old-format buildings: save this city
        private const string ArchivedKind = "archived"; // shapes only in the archive: restore them
        private const string RemainingKind = "remaining"; // converted save, manifest not empty: finish the old saves

        private UnsquareSettings _settings;
        private IBuildingDeformService _service;
        private bool _open;
        private int _count;
        private string _kind = ConvertKind;
        private string _result = string.Empty;
        private string _warning = string.Empty; // buildings to deform again after a retire
        private bool _done;

        protected override void OnCreate()
        {
            base.OnCreate();
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "legacySavesPending", () => _open));
            AddUpdateBinding(new GetterValueBinding<int>(Group, "legacySavesCount", () => _count));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "legacySavesKind", () => _kind));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "legacySavesResult", () => _result));
            AddUpdateBinding(new GetterValueBinding<bool>(Group, "legacySavesDone", () => _done));
            AddUpdateBinding(new GetterValueBinding<string>(Group, "legacySavesWarning", () => _warning));
            AddBinding(new TriggerBinding(Group, "closeLegacySaves", () => _open = false));
            AddBinding(new TriggerBinding(Group, "retireLegacySaves", Retire));
            AddBinding(new TriggerBinding(Group, "restoreLegacySaves", Restore));
        }

        protected override void OnDestroy()
        {
            if (_service != null)
                _service.LegacySaveLoaded -= OnLegacySaveLoaded;
            base.OnDestroy();
        }

        /// <summary>Hooks up the settings (hiding the convert reminder) and the service that reports old saves.</summary>
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

        // Missing shapes always ask: saving the city before restoring would keep the boxes. The reminders can be hidden.
        private void OnLegacySaveLoaded(LegacySaveCounts counts)
        {
            if (counts.Archived > 0)
                Open(ArchivedKind, counts.Archived);
            else if (_settings != null && _settings.HideLegacySavesPrompt)
                return;
            else if (counts.Convertible > 0)
                Open(ConvertKind, counts.Convertible);
            else if (counts.Remaining > 0)
                Open(RemainingKind, counts.Remaining);
        }

        private void Open(string kind, int count)
        {
            _kind = kind;
            _count = count;
            _result = string.Empty;
            _warning = string.Empty;
            _done = false;
            _open = true;
        }

        private void Retire()
        {
            if (_service == null)
                return;
            var result = _service.RetireLegacyManifest();
            _done = result.Done;
            _result = result.Summary;
            _warning = result.Warning ?? string.Empty;
        }

        private void Restore()
        {
            if (_service == null)
                return;
            var result = _service.RestoreArchivedShapes();
            _done = result.Done;
            _result = result.ToString(); // the dialog shows its own text when done
        }
    }
}
