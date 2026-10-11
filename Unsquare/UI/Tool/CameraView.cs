using Unity.Mathematics;
using UnityEngine;
using Unsquare.Core.Editing;

namespace Unsquare.UI.Tool
{
    /// <summary>
    /// The main camera of this frame: position and screen scale. Shared by the tool and the overlay so the
    /// pick radius and the drawn gizmo always agree.
    /// </summary>
    internal readonly struct CameraView
    {
        private CameraView(Camera camera, float3 position, ViewScale scale)
        {
            Camera = camera;
            Position = position;
            Scale = scale;
        }

        public Camera Camera { get; }
        public float3 Position { get; }
        public ViewScale Scale { get; }

        public static bool TryCapture(out CameraView view)
        {
            view = default;
            var camera = Camera.main;
            if (camera == null || Screen.height <= 0 || !(camera.fieldOfView > 0f && camera.fieldOfView < 180f))
                return false;
            view = new CameraView(camera, camera.transform.position, new ViewScale(camera.fieldOfView, Screen.height));
            return true;
        }
    }
}
