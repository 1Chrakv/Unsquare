using System;
using System.Collections.Generic;
using Colossal.Localization;
using Unsquare.Core.Naming;

namespace Unsquare.Integration.Prefabs
{
    /// <summary>
    /// Gives variant prefabs the names and descriptions of their base: the game looks them up by prefab name, and no
    /// locale has entries for variant names. Aliases are re-added whenever the active language is (re)loaded.
    /// </summary>
    internal sealed class VariantLocalization : IDisposable
    {
        private readonly LocalizationManager _localization;
        private readonly Dictionary<string, string> _baseNames = new(StringComparer.Ordinal);

        public VariantLocalization(LocalizationManager localization)
        {
            _localization = localization;
            if (_localization != null)
                _localization.onActiveDictionaryChanged += AddAll;
        }

        public void Register(string variantName, string baseName)
        {
            if (_baseNames.TryGetValue(variantName, out string known) && known == baseName)
                return;
            _baseNames[variantName] = baseName;
            Add(variantName, baseName);
        }

        public void Dispose()
        {
            if (_localization != null)
                _localization.onActiveDictionaryChanged -= AddAll;
        }

        private void AddAll()
        {
            foreach (var pair in _baseNames)
                Add(pair.Key, pair.Value);
        }

        private void Add(string variantName, string baseName)
        {
            try
            {
                var dictionary = _localization?.activeDictionary;
                if (dictionary == null)
                    return;
                foreach (var (variantKey, baseKey) in VariantLocaleKeys.Aliases(variantName, baseName))
                {
                    if (!dictionary.ContainsID(variantKey) && dictionary.TryGetValue(baseKey, out string text))
                        dictionary.Add(variantKey, text);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, $"Adding locale names for variant {variantName} failed; it shows its raw name.");
            }
        }
    }
}
