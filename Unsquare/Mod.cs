using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Buildings;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Unsquare.Api;
using Unsquare.Integration.Systems;
using Unsquare.UI.Bindings;
using Unsquare.UI.Editing;
using Unsquare.UI.Input;
using Unsquare.UI.Overlay;
using Unsquare.UI.Tool;

namespace Unsquare
{
    public class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger($"{nameof(Unsquare)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        private UnsquareSettings _settings;
        private SettingsApplier _settingsApplier;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info($"Unsquare {BuildInfo.Summary} loading");

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            _settings = new UnsquareSettings(this);
            _settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(_settings));
            _settings.RegisterKeyBindings();
            AssetDatabase.global.LoadSettings(nameof(Unsquare), _settings, new UnsquareSettings(this));

            // Before PrefabInitializeSystem, so prefabs added by the variant system initialize in the same frame.
            updateSystem.UpdateBefore<VariantSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            // After LotHeightSystem rewrote lot heights, before TerrainSystem draws them (ModificationEnd).
            updateSystem.UpdateAfter<LotLiftSystem, LotHeightSystem>(SystemUpdatePhase.Modification5);
            updateSystem.UpdateAt<BaseTerrainSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<CageEditorSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<UnsquareToolSystem, CageEditorSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<CageHotkeySystem, UnsquareToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAfter<CageOverlaySystem, CageHotkeySystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<UnsquareUISystem>(SystemUpdatePhase.UIUpdate);
            var world = updateSystem.World;
            world.GetOrCreateSystemManaged<CageHotkeySystem>().Bind(_settings);
            var session = world.GetOrCreateSystemManaged<CageEditorSystem>().Session;
            _settings.BindFlexItImport(() => session.ImportFlexIt().ToString());
            _settingsApplier = new SettingsApplier(_settings, UnsquareApi.GetDeformService(world), session,
                world.GetOrCreateSystemManaged<CageOverlaySystem>());
            _settingsApplier.Apply();
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            _settingsApplier?.Dispose();
            _settingsApplier = null;
            if (_settings != null)
            {
                _settings.UnregisterInOptionsUI();
                _settings = null;
            }
        }
    }
}
