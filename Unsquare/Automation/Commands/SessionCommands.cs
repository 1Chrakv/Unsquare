#if UNSQUARE_AUTOMATION
using System;
using Newtonsoft.Json.Linq;
using Unsquare.Api;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;
using Unsquare.Core.Editing;
using Unsquare.Core.Terrain;
using Unsquare.UI.Editing;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// The edit session's commands, the same ones mouse, hotkeys and tool window call. Commands that return a
    /// <see cref="DeformRequestResult"/> answer 422 when the mod refuses them, with the result in the body.
    /// </summary>
    internal sealed class SessionCommands
    {
        private readonly BridgeContext _context;

        public SessionCommands(BridgeContext context) => _context = context;

        private CageEditSession Session => _context.Session;

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET session", _ => CommandRun.Done(SessionJson.Session(_context)));
            routes.Add("POST session/mode", Mode);
            routes.Add("POST session/select", Select);
            routes.Add("POST session/clearTarget", InMode(_ => Session.ClearTarget()));
            routes.Add("POST session/stepBack", InModeResult(_ => Session.StepBack().ToString()));
            routes.Add("POST session/pick", InMode(args => Session.SelectKind(args.Enum<CageHandleKind>("kind"))));
            routes.Add("POST session/handle", Handle);
            routes.Add("POST session/nextHandle", InMode(_ => Session.NextHandle()));
            routes.Add("POST session/previousHandle", InMode(_ => Session.PreviousHandle()));
            routes.Add("POST session/nudge", InModeEdit(args => Session.Nudge(args.Float3("direction"))));
            routes.Add("POST session/step", InModeResult(_ =>
            {
                Session.CycleStep();
                return Session.Step.Meters.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }));
            routes.Add("POST session/drag", Drag);
            routes.Add("POST session/undo", InMode(_ => Session.Undo()));
            routes.Add("POST session/redo", InMode(_ => Session.Redo()));
            routes.Add("POST session/reset", InModeEdit(_ => Session.Reset()));
            routes.Add("POST session/resetHandle", InModeEdit(_ => Session.ResetHandle()));
            routes.Add("POST session/resetAll", InModeResult(_ => Session.ResetAll().ToString()));
            routes.Add("POST session/clearHistory", _ =>
            {
                Session.ClearHistory();
                return CommandRun.Done();
            });
            routes.Add("POST session/terrain", InModeEdit(_ => Session.ToggleTerrainLift()));
            routes.Add("POST session/fit", InModeEdit(_ => Session.FitToGround()));
            routes.Add("POST session/fitMode", InMode(args => Session.SetGroundFitMode(args.Enum<GroundFitMode>("mode"))));
            routes.Add("POST session/align", InModeEdit(args => Session.AlignToNeighbour(args.Enum<AlignSide>("side"))));
            routes.Add("POST session/level", InModeEdit(args => Session.AlignGroundToNeighbour(args.Enum<AlignSide>("side"))));
            routes.Add("POST session/alignMode", InMode(args => Session.SetAlignMode(args.Enum<AlignMode>("mode"))));
            routes.Add("POST session/editMode", InMode(args => Session.SetEditMode(args.Enum<CageTarget>("mode"))));
            routes.Add("POST session/blend", InModeEdit(args => Session.SetBlendMode(args.Enum<BlendMode>("mode"))));
            routes.Add("POST session/link", InModeEdit(args => args.String("follow").ToLowerInvariant() switch
            {
                "lot" => Session.LinkBuildingToLot(),
                "building" => Session.LinkLotToBuilding(),
                var other => throw new BridgeArgumentException($"'follow' must be lot or building, not '{other}'."),
            }));
            routes.Add("POST session/gizmo", InMode(args => Session.SetGizmoMode(args.Enum<GizmoMode>("mode"))));
            routes.Add("POST session/describe", InModeResult(_ =>
            {
                var text = Session.Describe();
                Mod.Log.Info($"[Edit] {text}");
                return text;
            }));
        }

        private CommandRun Mode(BridgeArgs args)
        {
            var on = args.Bool("on");
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            if (_context.Tool.IsActive != on)
                _context.Tool.Toggle();
            // The tool switch takes effect in the tool system's next update.
            var until = UnityEngine.Time.frameCount + 2;
            return CommandRun.Pending(() =>
            {
                if (UnityEngine.Time.frameCount < until)
                    return null;
                return _context.Tool.IsActive == on
                    ? CommandRun.Done(new JObject { ["mode"] = on })
                    : CommandRun.Fail(409, $"Unsquare mode stayed {(on ? "off" : "on")}.");
            });
        }

        private CommandRun Select(BridgeArgs args)
        {
            if (!Session.IsEnabled)
                return NotInMode();
            if (!_context.TryResolve(args.String("building"), out var building, out var error))
                return CommandRun.Fail(404, error);
            return Session.SelectBuilding(building)
                ? CommandRun.Done(SessionJson.Building(_context, building))
                : CommandRun.Fail(422, Session.IsDecisionOpen
                    ? "Another mod's building: a decision is open."
                    : "Selection refused (a handle is selected or the building isn't deformable).");
        }

        private CommandRun Handle(BridgeArgs args)
        {
            if (!Session.IsEnabled)
                return NotInMode();
            if (!Session.HasTarget)
                return CommandRun.Fail(409, "No target building.");
            if (args.Bool("clear", false))
            {
                Session.ClearSelection();
                return CommandRun.Done();
            }
            var kind = args.Enum<CageHandleKind>("kind");
            var index = args.Int("index", 0);
            if (index < 0 || index >= CageTopology.CountOf(kind))
                return CommandRun.Fail(400, $"{kind} index must be 0..{CageTopology.CountOf(kind) - 1}.");
            Session.SelectKind(kind);
            Session.SelectHandle(kind switch
            {
                CageHandleKind.Corner => CageHandle.Corner(index),
                CageHandleKind.Edge => CageHandle.Edge(index),
                CageHandleKind.Face => CageHandle.Face(index),
                _ => CageHandle.Cage(),
            });
            return CommandRun.Done(new JObject { ["handle"] = Session.Selection.Handle.ToString() });
        }

        /// <summary>
        /// Body: translate [x,y,z] (metres, building space) or rotate {axis 0..2, degrees}; frames (default 10);
        /// cancelAt (frame that right-clicks instead); part (gizmo part, only for highlighting).
        /// </summary>
        private CommandRun Drag(BridgeArgs args)
        {
            if (!Session.IsEnabled)
                return NotInMode();
            if (!Session.Selection.HasHandle)
                return CommandRun.Fail(409, "No handle selected.");
            var frames = args.Int("frames", 10);
            var cancelAt = args.Int("cancelAt", -1);
            ScriptedDrag drag;
            try
            {
                if (args.Has("rotate"))
                {
                    var rotate = args.Object("rotate");
                    drag = ScriptedDrag.Rotate(rotate.Int("axis"), rotate.Float("degrees"), frames, cancelAt);
                }
                else
                    drag = ScriptedDrag.Translate(args.Float3("translate"), frames, cancelAt);
            }
            catch (ArgumentException e) when (e is not BridgeArgumentException)
            {
                throw new BridgeArgumentException(e.Message);
            }
            if (!Session.BeginDrag(args.Int("part", -1)))
                return CommandRun.Fail(409, "The drag did not start.");

            var frame = 0;
            DeformRequestResult? last = null;
            return CommandRun.Pending(() =>
            {
                frame++;
                if (drag.CancelsAt(frame))
                {
                    Session.CancelDrag();
                    return CommandRun.Done(new JObject { ["cancelled"] = true, ["frames"] = frame });
                }
                last = Session.UpdateDrag(drag.MotionAt(frame));
                if (frame < drag.Frames)
                    return null;
                Session.EndDrag();
                return CommandRun.Done(new JObject { ["result"] = last?.ToString(), ["frames"] = frame });
            });
        }

        private BridgeHandler InMode(Action<BridgeArgs> command) => args =>
        {
            if (!Session.IsEnabled)
                return NotInMode();
            command(args);
            return CommandRun.Done();
        };

        private BridgeHandler InModeResult(Func<BridgeArgs, string> command) => args =>
            Session.IsEnabled ? CommandRun.Done(new JObject { ["result"] = command(args) }) : NotInMode();

        private BridgeHandler InModeEdit(Func<BridgeArgs, DeformRequestResult> command) => args =>
        {
            if (!Session.IsEnabled)
                return NotInMode();
            if (!Session.HasTarget)
                return CommandRun.Fail(409, "No target building.");
            var result = command(args);
            var body = new JObject { ["result"] = result.ToString() };
            return result is DeformRequestResult.Accepted or DeformRequestResult.Clamped
                ? CommandRun.Done(body)
                : CommandRun.Fail(422, $"{result}", body);
        };

        private static CommandRun NotInMode() => CommandRun.Fail(409, "Unsquare mode is off (POST session/mode).");
    }
}
#endif
