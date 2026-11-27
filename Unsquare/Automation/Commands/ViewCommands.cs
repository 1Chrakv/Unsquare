#if UNSQUARE_AUTOMATION
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game;
using Game.SceneFlow;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEngine;
using Unsquare.Core.Automation;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation.Commands
{
    /// <summary>
    /// What the player sees: the game camera, the UI's visibility, screenshots and where handles and gizmo parts are
    /// on screen. Screenshots go only to <c>automation\shots</c> in the mod's data folder.
    /// </summary>
    internal sealed class ViewCommands
    {
        private const int CameraSettleFrames = 30;
        private const int UiToggleFrames = 3;
        private const float CropPadding = 0.2f;
        private const int CropMinPixels = 240;
        private static readonly Regex ShotName = new("^[A-Za-z0-9_-]{1,80}$");

        private readonly BridgeContext _context;
        private readonly ScreenPoints _points;

        public ViewCommands(BridgeContext context)
        {
            _context = context;
            _points = new ScreenPoints(context);
        }

        public static string ShotsDirectory => Path.Combine(BridgeConfig.Directory, "shots");

        public void Register(RouteTable<BridgeHandler> routes)
        {
            routes.Add("GET view/camera", GetCamera);
            routes.Add("POST view/camera", SetCamera);
            routes.Add("POST view/ui", SetUi);
            routes.Add("POST view/screenshot", Screenshot);
            routes.Add("GET view/screenPoint", ScreenPoint);
        }

        private static bool TryController(out CameraController controller) =>
            CameraController.TryGet(out controller) && controller != null;

        private CommandRun GetCamera(BridgeArgs args)
        {
            if (!TryController(out var controller))
                return CommandRun.Fail(409, "No game camera (not in a city?).");
            return CommandRun.Done(CameraJson(controller));
        }

        /// <summary>
        /// Orbits the game camera around a building or point: yaw (0 looks along +Z, or with a building at its street
        /// front), pitch and distance in degrees and metres. Waits until the camera stops moving.
        /// </summary>
        private CommandRun SetCamera(BridgeArgs args)
        {
            if (!BridgeContext.InGame || !TryController(out var controller))
                return CommandRun.Fail(409, "Not in a loaded city.");
            float3 pivot = controller.pivot;
            float yawBase = 0f;
            if (args.Has("focus"))
            {
                if (!_context.TryResolve(args.String("focus"), out var building, out var error))
                    return CommandRun.Fail(404, error);
                var transform = _context.EntityManager.GetComponentData<Transform>(building);
                pivot = transform.m_Position;
                // The camera sits on the street side (+Z in building space) at yaw 0.
                float3 front = math.rotate(transform.m_Rotation, new float3(0f, 0f, 1f));
                yawBase = math.degrees(math.atan2(-front.x, -front.z));
            }
            else if (args.Has("point"))
                pivot = args.Float3("point");

            float2 angle = controller.angle;
            if (args.Has("yaw"))
                angle.x = Wrap(yawBase + args.Float("yaw"));
            if (args.Has("pitch"))
                angle.y = args.Float("pitch");
            controller.pivot = pivot;
            controller.angle = angle;
            if (args.Has("distance"))
                controller.zoom = math.clamp(args.Float("distance"), controller.zoomRange.min, controller.zoomRange.max);

            int until = Time.frameCount + CameraSettleFrames;
            return CommandRun.Pending(() =>
            {
                if (Time.frameCount < until || controller.moving)
                    return null;
                return CommandRun.Done(CameraJson(controller));
            });
        }

        private static float Wrap(float degrees) => (degrees % 360f + 540f) % 360f - 180f;

        private static JObject CameraJson(CameraController controller) => new()
        {
            ["pivot"] = BridgeContext.Json(controller.pivot),
            ["yaw"] = BridgeContext.Round(controller.angle.x),
            ["pitch"] = BridgeContext.Round(controller.angle.y),
            ["distance"] = BridgeContext.Round(controller.zoom),
            ["position"] = BridgeContext.Json(controller.cameraPosition),
            ["moving"] = controller.moving,
        };

        private static CommandRun SetUi(BridgeArgs args)
        {
            var visible = args.Bool("visible");
            var ui = GameManager.instance.userInterface;
            if (ui == null || ui.view == null)
                return CommandRun.Fail(409, "No game UI.");
            ui.view.enabled = visible;
            int until = Time.frameCount + UiToggleFrames;
            return CommandRun.Pending(() =>
                Time.frameCount < until ? null : CommandRun.Done(new JObject { ["visible"] = ui.view.enabled }));
        }

        /// <summary>
        /// A JPEG of the next finished frame. crop: "screen" (default), "target" (the edited cage, padded) or
        /// {x, y, width, height}; maxPx scales the longest side down; ui false hides the game UI for the shot.
        /// </summary>
        private CommandRun Screenshot(BridgeArgs args)
        {
            var name = args.String("name");
            if (!ShotName.IsMatch(name))
                return CommandRun.Fail(400, "name must be 1-80 of A-Z a-z 0-9 _ -.");
            if (!TryCrop(args, out var crop, out var cropError))
                return CommandRun.Fail(409, cropError);
            int maxPixels = args.Int("maxPx", 0);
            bool hideUi = !args.Bool("ui", true);
            var ui = GameManager.instance.userInterface;
            bool uiWasVisible = ui != null && ui.view != null && ui.view.enabled;
            if (hideUi && uiWasVisible)
                ui.view.enabled = false;
            var path = Path.Combine(ShotsDirectory, name + ".jpg");
            int startAt = Time.frameCount + (hideUi ? UiToggleFrames : 0);
            FrameCapture.Request request = null;
            return CommandRun.Pending(() =>
            {
                if (Time.frameCount < startAt)
                    return null;
                request ??= FrameCapture.Capture(path, crop, maxPixels);
                if (!request.Done)
                    return null;
                if (hideUi && uiWasVisible)
                    ui.view.enabled = true;
                if (request.Error != null)
                    return CommandRun.Fail(500, $"Screenshot failed: {request.Error}");
                var json = new JObject
                {
                    ["path"] = path,
                    ["width"] = request.Width,
                    ["height"] = request.Height,
                    ["screen"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height },
                };
                if (crop.HasValue)
                    json["crop"] = new JObject
                    {
                        ["x"] = crop.Value.X, ["y"] = crop.Value.Y,
                        ["width"] = crop.Value.Width, ["height"] = crop.Value.Height,
                    };
                return CommandRun.Done(json);
            });
        }

        private bool TryCrop(BridgeArgs args, out PixelRect? crop, out string error)
        {
            crop = null;
            error = null;
            if (!args.Has("crop"))
                return true;
            if (args.IsObject("crop"))
            {
                var rect = args.Object("crop");
                if (!ScreenCrop.TryAround(new List<float2>
                    {
                        new(rect.Float("x"), rect.Float("y")),
                        new(rect.Float("x") + rect.Float("width"), rect.Float("y") + rect.Float("height")),
                    }, Screen.width, Screen.height, 0f, 1, out var explicitRect))
                {
                    error = "The crop lies off screen.";
                    return false;
                }
                crop = explicitRect;
                return true;
            }
            switch (args.String("crop"))
            {
                case "screen":
                    return true;
                case "target":
                    if (!_points.TryCageCorners(out var corners, out error))
                        return false;
                    var projected = new List<float2>();
                    foreach (var corner in corners)
                        if (ScreenPoints.TryProject(corner, out var pixels, out _))
                            projected.Add(pixels);
                    if (!ScreenCrop.TryAround(projected, Screen.width, Screen.height, CropPadding, CropMinPixels,
                            out var around))
                    {
                        error = "The target is not on screen.";
                        return false;
                    }
                    crop = around;
                    return true;
                default:
                    error = "crop must be \"screen\", \"target\" or {x, y, width, height}.";
                    return false;
            }
        }

        /// <summary>
        /// Where something is on screen (pixels, top-left origin): ?handle=Corner3, ?gizmo=arrowY|plane|ringX (of the
        /// selected handle, with the gizmo's origin and length) or ?world=x,y,z.
        /// </summary>
        private CommandRun ScreenPoint(BridgeArgs args)
        {
            if (!BridgeContext.InGame)
                return CommandRun.Fail(409, "Not in a loaded city.");
            if (args.Has("handle"))
            {
                if (!HandleName.TryParse(args.String("handle"), out var handle))
                    return CommandRun.Fail(400, $"Unknown handle '{args.String("handle")}'.");
                return _points.TryHandle(handle, out var world, out var error)
                    ? CommandRun.Done(ScreenPoints.Json(world))
                    : CommandRun.Fail(409, error);
            }
            if (args.Has("gizmo"))
            {
                if (!GizmoGrabPoint.TryParsePart(args.String("gizmo"), out var part))
                    return CommandRun.Fail(400, "gizmo must be arrowX|arrowY|arrowZ|plane|ringX|ringY|ringZ.");
                if (!_points.TryGizmo(part, out var world, out var gizmo, out var error))
                    return CommandRun.Fail(409, error);
                var json = ScreenPoints.Json(world);
                json["gizmo"] = gizmo;
                json["part"] = part;
                return CommandRun.Done(json);
            }
            if (args.Has("world"))
            {
                var parts = args.String("world").Split(',');
                if (parts.Length != 3 || !float.TryParse(parts[0], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var x) ||
                    !float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var y) ||
                    !float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var z))
                    return CommandRun.Fail(400, "world must be x,y,z.");
                return CommandRun.Done(ScreenPoints.Json(new float3(x, y, z)));
            }
            return CommandRun.Fail(400, "Give handle, gizmo or world.");
        }
    }
}
#endif
