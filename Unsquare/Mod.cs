using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;

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
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
        }
    }
}
