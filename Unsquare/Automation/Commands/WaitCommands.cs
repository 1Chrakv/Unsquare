#if UNSQUARE_AUTOMATION
using Newtonsoft.Json.Linq;
using Unity.Entities;
using Unsquare.Core.Automation;
using UnityEngine;

namespace Unsquare.Automation.Commands
{
    /// <summary>Waiting inside the game, so clients never poll: for frames or for a building's bake to settle.</summary>
    internal sealed class WaitCommands
    {
        private const int DefaultMaxFrames = 1800;

        private readonly BridgeContext _context;

        public WaitCommands(BridgeContext context) => _context = context;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("POST wait/frames", Frames);
            routes.Add("POST wait/settled", Settled);
        }

        private static CommandRun Frames(BridgeArgs args)
        {
            var until = Time.frameCount + args.Int("frames");
            return CommandRun.Pending(() => Time.frameCount < until ? null : CommandRun.Done());
        }

        /// <summary>
        /// Waits until the building (default: the target) has nothing pending and shows the requested shape, for at
        /// least minFrames (default 2) in a row; fails after maxFrames. Body: building, minFrames, maxFrames.
        /// </summary>
        private CommandRun Settled(BridgeArgs args)
        {
            Entity building;
            if (args.Has("building"))
            {
                if (!_context.TryResolve(args.String("building"), out building, out var error))
                    return CommandRun.Fail(404, error);
            }
            else if (_context.Session.HasTarget)
                building = _context.Session.Target;
            else
                return CommandRun.Fail(409, "No building given and no target.");

            var minFrames = args.Int("minFrames", 2);
            var deadline = Time.frameCount + args.Int("maxFrames", DefaultMaxFrames);
            var settledSince = -1;
            return CommandRun.Pending(() =>
            {
                if (!_context.EntityManager.Exists(building))
                    return CommandRun.Fail(404, "The building is gone.");
                if (!_context.Deform.TryGetCage(building, _context.Session.EditMode, out var cage))
                    return CommandRun.Fail(422, "The building is not deformable.");
                var settled = SessionJson.IsSettled(cage) && _context.Session.ActiveDrag == null;
                if (!settled)
                    settledSince = -1;
                else if (settledSince < 0)
                    settledSince = Time.frameCount;
                if (settled && Time.frameCount - settledSince >= minFrames)
                    return CommandRun.Done(new JObject { ["cage"] = SessionJson.Cage(cage) });
                return Time.frameCount > deadline
                    ? CommandRun.Fail(504, "The building did not settle.", new JObject { ["cage"] = SessionJson.Cage(cage) })
                    : null;
            });
        }
    }
}
#endif
