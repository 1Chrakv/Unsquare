using System.Collections.Generic;
using Colossal;

namespace Unsquare.UI.Input
{
    /// <summary>English texts for the options page and key bindings.</summary>
    internal sealed class LocaleEN : IDictionarySource
    {
        private readonly UnsquareSettings _settings;

        public LocaleEN(UnsquareSettings settings)
        {
            _settings = settings;
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            var entries = new Dictionary<string, string>
            {
                { _settings.GetSettingsLocaleID(), "Unsquare" },
                { _settings.GetOptionTabLocaleID(UnsquareSettings.MainTab), "Main" },
                { _settings.GetOptionGroupLocaleID(UnsquareSettings.HotkeyGroup), "Cage editing hotkeys" },
                { _settings.GetBindingMapLocaleID(), "Unsquare" },
            };
            foreach (var hotkey in CageHotkeys.All)
            {
                entries[_settings.GetOptionLabelLocaleID(hotkey.PropertyName)] = hotkey.Label;
                entries[_settings.GetOptionDescLocaleID(hotkey.PropertyName)] =
                    $"{hotkey.Label} on the building selected in the info panel.";
                entries[_settings.GetBindingKeyLocaleID(hotkey.ActionName)] = hotkey.Label;
            }
            return entries;
        }

        public void Unload()
        {
        }
    }
}
