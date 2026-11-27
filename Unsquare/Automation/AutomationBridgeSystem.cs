#if UNSQUARE_AUTOMATION
using System;
using Game;
using Newtonsoft.Json.Linq;
using Unsquare.Automation.Commands;
using Unsquare.Core.Automation;

namespace Unsquare.Automation
{
    /// <summary>
    /// Debug-only REST bridge for the in-game test suite (<c>IngameTests/</c>). Runs queued requests on the main
    /// thread, one at a time, in UI update so it also answers in the main menu. Off unless enabled on disk.
    /// </summary>
    public partial class AutomationBridgeSystem : GameSystemBase
    {
        private readonly RouteTable<BridgeHandler> _routes = new();
        private BridgeServer _server;
        private BridgeContext _context;
        private BridgeCall _current;
        private CommandRun _currentRun;

        protected override void OnCreate()
        {
            base.OnCreate();
            var config = BridgeConfig.Load();
            if (!config.Enabled)
            {
                Enabled = false;
                return;
            }
            _context = new BridgeContext(World, GetEntityQuery(BridgeContext.BuildingQuery));
            new GameCommands(_context).Register(_routes);
            new BuildingCommands(_context).Register(_routes);
            new SessionCommands(_context).Register(_routes);
            new WaitCommands(_context).Register(_routes);
            new ProbeCommands(_context).Register(_routes);
            new CityCommands(_context).Register(_routes);
            try
            {
                _server = new BridgeServer(config.Port, config.Token);
                _server.Start();
                Mod.Log.Info($"[Auto] Automation bridge listening on http://127.0.0.1:{config.Port}/v1/");
            }
            catch (Exception e)
            {
                Mod.Log.Error($"[Auto] Automation bridge could not listen on port {config.Port}: {e.Message}");
                _server = null;
                Enabled = false;
            }
        }

        /// <summary>Gives the bridge the options page's settings (they are created after the systems).</summary>
        internal void Bind(UI.Input.UnsquareSettings settings)
        {
            if (_context != null)
                new SettingsCommands(settings).Register(_routes);
        }

        protected override void OnDestroy()
        {
            _server?.Dispose();
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (_current == null)
            {
                if (!_server.TryDequeue(out _current))
                    return;
                Mod.Log.Info($"[Auto] >>> {_current.Id} {_current.Method} {_current.Path}");
                _currentRun = Start(_current);
            }
            else
                _currentRun = Guard(() => _currentRun.Poll());

            if (_currentRun.IsPending)
                return;
            Mod.Log.Info($"[Auto] <<< {_current.Id} {_currentRun.Status}{(_currentRun.Error != null ? " " + _currentRun.Error : "")}");
            _current.Completion.TrySetResult((_currentRun.Status, Reply(_currentRun)));
            _current = null;
            _currentRun = null;
        }

        private CommandRun Start(BridgeCall call)
        {
            if (!_routes.TryMatch(call.Method, call.Path, out var handler, out var parameters, out var methodAllowed))
                return methodAllowed
                    ? CommandRun.Fail(405, $"{call.Method} is not allowed on {call.Path}.")
                    : CommandRun.Fail(404, $"Unknown path {call.Path}.", new JArray(_routes.Patterns));
            var args = new BridgeArgs(call.Body, parameters, call.Query);
            return Guard(() => handler(args));
        }

        private static CommandRun Guard(Func<CommandRun> run)
        {
            try
            {
                return run();
            }
            catch (BridgeArgumentException e)
            {
                return CommandRun.Fail(400, e.Message);
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "[Auto] Command failed");
                return CommandRun.Fail(500, $"{e.GetType().Name}: {e.Message}");
            }
        }

        private JObject Reply(CommandRun run)
        {
            var body = new JObject
            {
                ["ok"] = run.Error == null,
                ["frame"] = UnityEngine.Time.frameCount,
            };
            if (run.Error != null)
                body["error"] = run.Error;
            if (run.Result != null)
                body["result"] = run.Result;
            if (BridgeContext.InGame)
            {
                try
                {
                    body["session"] = SessionJson.Session(_context);
                }
                catch (Exception e)
                {
                    body["session"] = $"unavailable: {e.Message}";
                }
            }
            return body;
        }
    }
}
#endif
