using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Unsquare.Integration.Systems;
using Unsquare.UI.Editing;
using Unsquare.UI.Input;
using Unsquare.UI.Overlay;

namespace Unsquare
{
    public class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger($"{nameof(Unsquare)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        private UnsquareSettings _settings;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            _settings = new UnsquareSettings(this);
            _settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(_settings));
            _settings.RegisterKeyBindings();
            AssetDatabase.global.LoadSettings(nameof(Unsquare), _settings, new UnsquareSettings(this));

            // Before PrefabInitializeSystem, so prefabs added by the variant system initialize in the same frame.
            updateSystem.UpdateBefore<VariantSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<CageEditorSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<CageHotkeySystem, CageEditorSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<CageOverlaySystem, CageHotkeySystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.World.GetOrCreateSystemManaged<CageHotkeySystem>().Bind(_settings);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            if (_settings != null)
            {
                _settings.UnregisterInOptionsUI();
                _settings = null;
            }
        }
    }
}
