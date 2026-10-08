using Colossal.Logging;
using Game;
using Game.Modding;
using Game.Prefabs;
using Game.SceneFlow;
using Unsquare.Integration.Systems;
using Unsquare.UI.Editing;

namespace Unsquare
{
    public class Mod : IMod
    {
        public static readonly ILog Log = LogManager.GetLogger($"{nameof(Unsquare)}.{nameof(Mod)}").SetShowsErrorsInUI(false);

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            // Before PrefabInitializeSystem, so prefabs added by the variant system initialize in the same frame.
            updateSystem.UpdateBefore<VariantSystem, PrefabInitializeSystem>(SystemUpdatePhase.PrefabUpdate);
            updateSystem.UpdateAt<CageEditorSystem>(SystemUpdatePhase.ToolUpdate);
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
        }
    }
}
