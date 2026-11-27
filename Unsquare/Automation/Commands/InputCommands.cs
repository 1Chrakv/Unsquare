#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using Game.Input;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using Unsquare.Core.Automation;
using Unsquare.UI.Input;
using InputManager = Game.Input.InputManager;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// Real input for tests: mouse moves, clicks and drags and key presses go through Unity's input system, so the
    /// game's input handling, the Unsquare tool and the hotkeys react as to the player. Targets can be pixels
    /// (top-left origin), cage handles, gizmo parts or world points.
    /// </summary>
    internal sealed class InputCommands
    {
        private const int DefaultDragFrames = 12;
        private const int DefaultHoldFrames = 2;
        private const int SettleFrames = 2;

        private readonly BridgeContext _context;
        private readonly UnsquareSettings _settings;
        private readonly SyntheticInput _input;
        private readonly ScreenPoints _points;

        public InputCommands(BridgeContext context, UnsquareSettings settings, SyntheticInput input)
        {
            _context = context;
            _settings = settings;
            _input = input;
            _points = new ScreenPoints(context);
        }

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET input/devices", _ => CommandRun.Done(_input.Describe()));
            routes.Add("POST input/config", Configure);
            routes.Add("POST input/reset", Reset);
            routes.Add("POST input/mouse", Mouse);
            routes.Add("POST input/key", Key);
            routes.Add("POST input/rebind", Rebind);
        }

        private CommandRun Configure(BridgeArgs args)
        {
            if (args.Has("via"))
            {
                var via = args.String("via");
                if (via != SyntheticInput.Virtual && via != SyntheticInput.Current)
                    return CommandRun.Fail(400, "via must be \"virtual\" or \"current\".");
                if (via != _input.Via)
                    _input.Reset();
                _input.Via = via;
            }
            if (args.Has("forceFocus"))
                _input.ForceFocus = args.Bool("forceFocus");
            return CommandRun.Done(_input.Describe());
        }

        private CommandRun Reset(BridgeArgs args)
        {
            _input.Reset();
            return CommandRun.Done(_input.Describe());
        }

        /// <summary>
        /// action move | press | release | click | drag; button left | right; at and to: {x, y}, {handle},
        /// {gizmo} or {world: [x, y, z]}. A drag presses at "at", moves to "to" over frames and releases.
        /// </summary>
        private CommandRun Mouse(BridgeArgs args)
        {
            var action = args.String("action");
            bool right = args.String("button", "left") == "right";
            float2 start = _input.Position, end = start;
            JObject resolved = null;
            if (args.Has("at") && !TryPoint(args.Object("at"), out start, out resolved, out var atError))
                return CommandRun.Fail(409, atError);
            if (action == "drag")
            {
                if (!args.Has("to"))
                    return CommandRun.Fail(400, "A drag needs \"to\".");
                if (!TryPoint(args.Object("to"), out end, out _, out var toError))
                    return CommandRun.Fail(409, toError);
            }

            var steps = new List<System.Action>();
            switch (action)
            {
                case "move":
                    steps.Add(() => _input.Pointer(start));
                    break;
                case "press":
                    steps.Add(() => _input.Pointer(start));
                    steps.Add(() => Press(right, true));
                    break;
                case "release":
                    steps.Add(() => Press(right, false));
                    break;
                case "click":
                    steps.Add(() => _input.Pointer(start));
                    steps.Add(() => Press(right, true));
                    for (int i = 1; i < args.Int("holdFrames", DefaultHoldFrames); i++)
                        steps.Add(() => { });
                    steps.Add(() => Press(right, false));
                    break;
                case "drag":
                    int frames = math.max(1, args.Int("frames", DefaultDragFrames));
                    steps.Add(() => _input.Pointer(start));
                    steps.Add(() => Press(right, true));
                    for (int i = 1; i <= frames; i++)
                    {
                        float t = (float)i / frames;
                        steps.Add(() => _input.Pointer(math.lerp(start, end, t)));
                    }
                    if (!args.Bool("hold", false))
                        steps.Add(() => Press(right, false));
                    break;
                default:
                    return CommandRun.Fail(400, "action must be move, press, release, click or drag.");
            }
            int settle = args.Int("settleFrames", SettleFrames);
            return Play(steps, settle, () => new JObject
            {
                ["pointer"] = new JArray(BridgeContext.Round(_input.Position.x), BridgeContext.Round(_input.Position.y)),
                ["target"] = resolved,
                ["hover"] = HoverJson(),
            });
        }

        private void Press(bool right, bool down)
        {
            if (right)
                _input.Buttons(right: down);
            else
                _input.Buttons(left: down);
        }

        private JObject HoverJson()
        {
            var hover = _context.Session.Hover;
            return new JObject
            {
                ["handle"] = hover.HasHandle ? hover.Handle.ToString() : null,
                ["part"] = hover.Axis,
            };
        }

        /// <summary>
        /// Presses a binding by name (its keys after any rebinding, e.g. UnsquareUndo) or raw keys by Unity name
        /// (["LeftCtrl"], ["Escape"]), holds them for holdFrames and releases.
        /// </summary>
        private CommandRun Key(BridgeArgs args)
        {
            var keys = new List<UnityEngine.InputSystem.Key>();
            string text;
            if (args.Has("binding"))
            {
                var name = args.String("binding");
                var proxy = _settings.GetAction(name);
                if (proxy == null)
                    return CommandRun.Fail(404, $"No binding {name}.");
                if (!_input.TryKeysOf(proxy, out keys, out text))
                    return CommandRun.Fail(409, $"{name} has no keyboard key.");
            }
            else
            {
                var names = new List<string>();
                foreach (var name in args.Strings("keys"))
                {
                    if (!SyntheticInput.TryKey(name, out var key))
                        return CommandRun.Fail(400, $"Unknown key {name}.");
                    keys.Add(key);
                    names.Add(name);
                }
                text = string.Join("+", names);
            }
            int hold = math.max(1, args.Int("holdFrames", DefaultHoldFrames));
            var steps = new List<System.Action>();
            // Modifiers first, like a player: the game's modifier checks look at the frame the key goes down.
            for (int i = 1; i <= keys.Count; i++)
            {
                var down = keys.GetRange(0, i);
                steps.Add(() => _input.Keys(down));
            }
            for (int i = 1; i < hold; i++)
                steps.Add(() => { });
            steps.Add(() => _input.Keys(new UnityEngine.InputSystem.Key[0]));
            return Play(steps, args.Int("settleFrames", SettleFrames), () => new JObject
            {
                ["keys"] = text,
            });
        }

        /// <summary>Rebinds a binding's key to a control path (e.g. &lt;Keyboard&gt;/k); returns the path it had.</summary>
        private CommandRun Rebind(BridgeArgs args)
        {
            var name = args.String("binding");
            var path = args.String("path");
            var proxy = _settings.GetAction(name);
            if (proxy == null)
                return CommandRun.Fail(404, $"No binding {name}.");
            foreach (var binding in proxy.bindings)
            {
                if (!binding.isKeyboard)
                    continue;
                var previous = binding.path;
                if (!InputManager.instance.SetBinding(binding.WithPath(path), out var result))
                    return CommandRun.Fail(422, $"The game refused {path} for {name}.");
                return CommandRun.Done(new JObject { ["binding"] = name, ["previous"] = previous, ["path"] = result.path });
            }
            return CommandRun.Fail(409, $"{name} has no keyboard binding.");
        }

        // One step per frame: events queued now reach the game's input update at the start of the next frame.
        private static CommandRun Play(List<System.Action> steps, int settleFrames, System.Func<JObject> result)
        {
            int next = 0, doneAt = -1;
            return CommandRun.Pending(() =>
            {
                if (next < steps.Count)
                {
                    steps[next++]();
                    return null;
                }
                if (doneAt < 0)
                    doneAt = Time.frameCount + settleFrames;
                return Time.frameCount < doneAt ? null : CommandRun.Done(result());
            });
        }

        private bool TryPoint(BridgeArgs at, out float2 pixels, out JObject resolved, out string error)
        {
            pixels = default;
            resolved = null;
            error = null;
            float3 world;
            if (at.Has("x"))
            {
                pixels = new float2(at.Float("x"), at.Float("y"));
                return true;
            }
            if (at.Has("handle"))
            {
                if (!HandleName.TryParse(at.String("handle"), out var handle))
                {
                    error = $"Unknown handle '{at.String("handle")}'.";
                    return false;
                }
                if (!_points.TryHandle(handle, out world, out error))
                    return false;
            }
            else if (at.Has("gizmo"))
            {
                if (!GizmoGrabPoint.TryParsePart(at.String("gizmo"), out var part))
                {
                    error = "gizmo must be arrowX|arrowY|arrowZ|plane|ringX|ringY|ringZ.";
                    return false;
                }
                if (!_points.TryGizmo(part, out world, out _, out error))
                    return false;
            }
            else if (at.Has("world"))
                world = at.Float3("world");
            else
            {
                error = "A point needs x/y, handle, gizmo or world.";
                return false;
            }
            resolved = ScreenPoints.Json(world);
            if (!ScreenPoints.TryProject(world, out pixels, out var onScreen) || !onScreen)
            {
                error = "The point is not on screen.";
                return false;
            }
            return true;
        }
    }
}
#endif
