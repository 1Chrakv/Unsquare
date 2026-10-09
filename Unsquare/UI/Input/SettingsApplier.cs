using System;
using Game.Settings;
using Unsquare.Api;
using Unsquare.UI.Editing;
using Unsquare.UI.Overlay;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// Pushes the advanced options into the deform service, the edit session and the overlay: once on load
    /// and again whenever the options page applies a change.
    /// </summary>
    internal sealed class SettingsApplier : IDisposable
    {
        private readonly UnsquareSettings _settings;
        private readonly IBuildingDeformService _service;
        private readonly CageEditSession _session;
        private readonly CageOverlaySystem _overlay;
        private string _lastSummary;

        public SettingsApplier(UnsquareSettings settings, IBuildingDeformService service, CageEditSession session,
            CageOverlaySystem overlay)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _settings.onSettingsApplied += OnSettingsApplied;
        }

        public void Apply()
        {
            try
            {
                var pipeline = _settings.ToPipelineOptions();
                var edit = _settings.ToEditOptions();
                _service.Configure(pipeline);
                _session.Configure(edit);
                _overlay.Style = _settings.ToOverlayStyle();

                // The options page applies on every slider step; log only real changes.
                string summary = $"{edit}; {pipeline}";
                if (summary != _lastSummary)
                    Mod.Log.Info($"Options: {summary}");
                _lastSummary = summary;
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Applying options failed; previous options stay active.");
            }
        }

        public void Dispose() => _settings.onSettingsApplied -= OnSettingsApplied;

        private void OnSettingsApplied(Setting setting) => Apply();
    }
}
