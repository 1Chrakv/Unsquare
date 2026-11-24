#if UNSQUARE_AUTOMATION
using System;
using System.Threading.Tasks;
using Colossal;
using Colossal.IO.AssetDatabase;
using Colossal.Serialization.Entities;
using Game;
using Game.Assets;
using Game.Prefabs.Modes;
using Game.SceneFlow;
using Game.Simulation;
using Game.UI;
using Game.UI.Menu;
using Newtonsoft.Json.Linq;
using Unsquare.Core.Automation;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unsquare.Automation.Commands
{
    /// <summary>Game lifecycle: identity, state, loading and saving saves, simulation speed, quitting.</summary>
    internal sealed class GameCommands
    {
        /// <summary>Tests save only under this prefix, and never over the fixture they load.</summary>
        public const string SavePrefix = "UQTest-";
        public const string FixtureSave = "UQTest-Fixture";

        // Frames after a load finishes before the bridge reports it: VariantSystem restores saved cages in its first
        // update after OnGameLoaded and swaps them a few frames later.
        private const int SettleFramesAfterLoad = 60;

        private readonly BridgeContext _context;
        private string _loadedSave;

        public GameCommands(BridgeContext context) => _context = context;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET ping", Ping);
            routes.Add("GET state", State);
            routes.Add("POST game/load", Load);
            routes.Add("POST game/save", Save);
            routes.Add("POST game/mainMenu", MainMenu);
            routes.Add("POST game/quit", Quit);
            routes.Add("POST game/speed", Speed);
            routes.Add("POST log/mark", Mark);
        }

        // The bridge answers while the game still starts; a load then would be replaced by its main menu.
        private static bool IsReady =>
            !GameManager.instance.isGameLoading &&
            (GameManager.instance.gameMode == GameMode.MainMenu || GameManager.instance.gameMode.IsGame());

        private CommandRun Ping(BridgeArgs args) => CommandRun.Done(new JObject
        {
            ["mod"] = "Unsquare",
            ["version"] = BuildInfo.Version,
            ["configuration"] = BuildInfo.Configuration,
            ["builtUtc"] = BuildInfo.BuildTimeUtc,
        });

        private CommandRun State(BridgeArgs args)
        {
            var manager = GameManager.instance;
            var simulation = _context.World.GetExistingSystemManaged<SimulationSystem>();
            return CommandRun.Done(new JObject
            {
                ["gameMode"] = manager.gameMode.ToString(),
                ["loading"] = manager.isGameLoading,
                ["ready"] = IsReady,
                ["inGame"] = BridgeContext.InGame,
                ["save"] = _loadedSave,
                ["frame"] = Time.frameCount,
                ["simulationFrame"] = simulation?.frameIndex,
                ["speed"] = simulation?.selectedSpeed,
            });
        }

        private CommandRun Load(BridgeArgs args)
        {
            var name = args.String("save");
            if (!IsReady)
                return CommandRun.Fail(409, "The game is still starting or loading; wait until state.ready.");
            if (!TryFindSave(name, out var save))
                return CommandRun.Fail(404, $"No save named '{name}'.");

            Task<bool> loading = null;
            TaskManager.instance.EnqueueTask("SaveLoadGame", () => loading = StartLoad(save), 1);
            var settleFrames = -1;
            return CommandRun.Pending(() =>
            {
                if (loading == null || !loading.IsCompleted)
                    return null;
                if (loading.IsFaulted || loading.IsCanceled || !loading.Result)
                    return CommandRun.Fail(500, $"Loading '{name}' failed: {loading.Exception?.GetBaseException().Message}");
                if (!BridgeContext.InGame)
                    return null;
                if (settleFrames < 0)
                    settleFrames = Time.frameCount + SettleFramesAfterLoad;
                if (Time.frameCount < settleFrames)
                    return null;
                _loadedSave = name;
                return CommandRun.Done(new JObject { ["save"] = name });
            });
        }

        private Task<bool> StartLoad(SaveGameMetadata save)
        {
            // What the main menu prepares before loading a save.
            var info = save.target;
            var mapMetadata = _context.World.GetOrCreateSystemManaged<MapMetadataSystem>();
            mapMetadata.mapName = info.mapName;
            mapMetadata.prefabReferences = info.prefabReferences;
            _context.World.GetOrCreateSystemManaged<GameModeSystem>().overrideMode = info.gameMode;
            return GameManager.instance.Load(GameMode.Game, Purpose.LoadGame, save);
        }

        private CommandRun Save(BridgeArgs args)
        {
            var name = args.String("name");
            if (!name.StartsWith(SavePrefix, StringComparison.Ordinal) || name == FixtureSave)
                return CommandRun.Fail(400, $"Tests save only as '{SavePrefix}…', never as '{FixtureSave}'.");
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");

            Task saving = null;
            TaskManager.instance.EnqueueTask("SaveLoadGame", () => saving = SaveAs(name), 1);
            return CommandRun.Pending(() =>
            {
                if (saving == null || !saving.IsCompleted)
                    return null;
                return saving.IsFaulted
                    ? CommandRun.Fail(500, $"Saving '{name}' failed: {saving.Exception?.GetBaseException().Message}")
                    : CommandRun.Done(new JObject { ["save"] = name });
            });
        }

        private async Task SaveAs(string name)
        {
            var preview = ScreenCaptureHelper.CreateRenderTarget("UnsquareTestPreview", 680, 383);
            try
            {
                ScreenCaptureHelper.CaptureScreenshot(Camera.main, preview, new MenuHelpers.SaveGamePreviewSettings());
                var database = AssetDatabase.user;
                if (database.Exists<PackageAsset>(SaveHelpers.GetAssetDataPath<SaveGameMetadata>(database, name),
                        out var existing))
                    database.DeleteAsset(existing);
                var info = _context.World.GetOrCreateSystemManaged<MenuUISystem>().GetSaveInfo(autoSave: false);
                await GameManager.instance.Save(name, info, database, preview);
            }
            finally
            {
                Object.Destroy(preview);
            }
        }

        private CommandRun MainMenu(BridgeArgs args)
        {
            Task<bool> leaving = null;
            TaskManager.instance.EnqueueTask("SaveLoadGame", () => leaving = GameManager.instance.MainMenu(), 1);
            return CommandRun.Pending(() =>
            {
                if (leaving == null || !leaving.IsCompleted || GameManager.instance.gameMode != GameMode.MainMenu)
                    return null;
                _loadedSave = null;
                return CommandRun.Done();
            });
        }

        private static CommandRun Quit(BridgeArgs args)
        {
            Mod.Log.Info("[Auto] Quitting the game");
            GameManager.QuitGame();
            return CommandRun.Done();
        }

        private CommandRun Speed(BridgeArgs args)
        {
            var simulation = _context.World.GetExistingSystemManaged<SimulationSystem>();
            if (simulation == null || !BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            simulation.selectedSpeed = args.Float("speed");
            return CommandRun.Done(new JObject { ["speed"] = simulation.selectedSpeed });
        }

        private static CommandRun Mark(BridgeArgs args)
        {
            Mod.Log.Info($"[Auto] --- {args.String("text")}");
            return CommandRun.Done();
        }

        private static bool TryFindSave(string name, out SaveGameMetadata save)
        {
            save = null;
            foreach (var candidate in AssetDatabase.global.GetAssets(default(SearchFilter<SaveGameMetadata>)))
            {
                if (candidate == null || !candidate.isValidSaveGame)
                    continue;
                if (candidate.name == name || candidate.target?.displayName == name)
                {
                    save = candidate;
                    return true;
                }
            }
            return false;
        }
    }
}
#endif
