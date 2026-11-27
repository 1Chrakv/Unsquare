#if UNSQUARE_AUTOMATION
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEngine;
using Unsquare.Api;
using Unsquare.Core.Automation;
using Unsquare.Core.Cage;
using Unsquare.UI.Overlay;
using Unsquare.UI.Tool;
using Transform = Game.Objects.Transform;

namespace Unsquare.Automation
{
    /// <summary>
    /// Projects world points, handles and gizmo parts of the edited building onto the screen, in pixels with the
    /// origin top left (like the UI's DOM), so tests can point the mouse at them.
    /// </summary>
    internal sealed class ScreenPoints
    {
        private readonly BridgeContext _context;

        public ScreenPoints(BridgeContext context) => _context = context;

        /// <summary>Pixels (top-left origin) of a world point; false behind the camera or without one.</summary>
        public static bool TryProject(float3 world, out float2 pixels, out bool onScreen)
        {
            pixels = default;
            onScreen = false;
            if (!CameraView.TryCapture(out var view))
                return false;
            Vector3 screen = view.Camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
                return false;
            pixels = new float2(screen.x, Screen.height - screen.y);
            onScreen = pixels.x >= 0f && pixels.x < Screen.width && pixels.y >= 0f && pixels.y < Screen.height;
            return true;
        }

        public bool TryHandle(CageHandle handle, out float3 world, out string error)
        {
            world = default;
            if (!TryTargetCage(out var transform, out var cage, out error))
                return false;
            world = BuildingSpace.HandleWorldPosition(transform, cage, handle);
            return true;
        }

        /// <summary>The grab point of a gizmo part of the selected handle, plus the gizmo's origin and length.</summary>
        public bool TryGizmo(int part, out float3 world, out JObject gizmo, out string error)
        {
            world = default;
            gizmo = null;
            if (!TryTargetCage(out var transform, out var cage, out error))
                return false;
            var selection = _context.Session.Selection;
            if (!selection.HasHandle)
            {
                error = "No handle selected: there is no gizmo.";
                return false;
            }
            if (!CameraView.TryCapture(out var view))
            {
                error = "No camera.";
                return false;
            }
            float3 origin = cage.HandlePosition(selection.Handle);
            float3 originWorld = BuildingSpace.PointToWorld(transform, origin);
            var style = _context.World.GetOrCreateSystemManaged<CageOverlaySystem>().Style;
            float length = style.GizmoLength(math.distance(view.Position, originWorld), view.Scale);
            float3 eye = BuildingSpace.PointToBuilding(transform, view.Position);
            if (!GizmoGrabPoint.TryGet(selection.Handle, part, origin, length, eye, out var local))
            {
                error = $"{selection.Handle} has no such ring.";
                return false;
            }
            world = BuildingSpace.PointToWorld(transform, local);
            gizmo = new JObject
            {
                ["origin"] = BridgeContext.Json(originWorld),
                ["length"] = BridgeContext.Round(length),
                ["rings"] = _context.Session.ShowsRings,
            };
            return true;
        }

        /// <summary>World corners of the target's cage, for crops and on-screen checks.</summary>
        public bool TryCageCorners(out float3[] corners, out string error)
        {
            corners = null;
            if (!TryTargetCage(out var transform, out var cage, out error))
                return false;
            corners = new float3[CageTopology.CornerCount];
            for (int i = 0; i < corners.Length; i++)
                corners[i] = BuildingSpace.PointToWorld(transform, cage.CornerPosition(i));
            return true;
        }

        public static JObject Json(float3 world)
        {
            var json = new JObject { ["world"] = BridgeContext.Json(world), ["screen"] = new JObject
            {
                ["width"] = Screen.width,
                ["height"] = Screen.height,
            } };
            if (TryProject(world, out var pixels, out var onScreen))
            {
                json["x"] = BridgeContext.Round(pixels.x);
                json["y"] = BridgeContext.Round(pixels.y);
                json["onScreen"] = onScreen;
            }
            else
                json["onScreen"] = false;
            return json;
        }

        private bool TryTargetCage(out Transform transform, out CageState cage, out string error)
        {
            transform = default;
            cage = default;
            error = null;
            var session = _context.Session;
            if (!session.HasTarget || !session.TryGetCage(out cage) ||
                !BuildingSpace.TryGetTransform(_context.EntityManager, session.Target, out transform))
            {
                error = "No target building with a cage.";
                return false;
            }
            return true;
        }
    }
}
#endif
