#if UNSQUARE_AUTOMATION
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Unsquare.Core.Automation;
using Object = UnityEngine.Object;

namespace Unsquare.Automation
{
    /// <summary>
    /// Takes the bridge's screenshots: the finished frame (world, overlay and, unless hidden, the UI) read at the end
    /// of the frame, cropped, scaled down and written as JPEG. A hidden object runs the coroutine.
    /// </summary>
    internal sealed class FrameCapture : MonoBehaviour
    {
        private const int JpegQuality = 85;

        /// <summary>A capture in flight; done once <see cref="Done"/> is set, failed if <see cref="Error"/> is.</summary>
        internal sealed class Request
        {
            public Request(string path, PixelRect? crop, int maxPixels)
            {
                TargetPath = path;
                Crop = crop;
                MaxPixels = maxPixels;
            }

            public string TargetPath { get; }
            public PixelRect? Crop { get; }
            public int MaxPixels { get; }
            public bool Done { get; internal set; }
            public string Error { get; internal set; }
            public int Width { get; internal set; }
            public int Height { get; internal set; }
        }

        private static FrameCapture _instance;

        public static Request Capture(string path, PixelRect? crop, int maxPixels)
        {
            if (_instance == null)
            {
                var host = new GameObject("UnsquareAutomationCapture") { hideFlags = HideFlags.HideAndDontSave };
                Object.DontDestroyOnLoad(host);
                _instance = host.AddComponent<FrameCapture>();
            }
            var request = new Request(path, crop, maxPixels);
            _instance.StartCoroutine(_instance.Run(request));
            return request;
        }

        private IEnumerator Run(Request request)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = null, cropped = null, scaled = null;
            try
            {
                frame = ScreenCapture.CaptureScreenshotAsTexture();
                cropped = request.Crop.HasValue ? CropTo(frame, request.Crop.Value) : frame;
                scaled = ScaleDown(cropped, request.MaxPixels);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(request.TargetPath));
                File.WriteAllBytes(request.TargetPath, scaled.EncodeToJPG(JpegQuality));
                request.Width = scaled.width;
                request.Height = scaled.height;
            }
            catch (Exception e)
            {
                request.Error = $"{e.GetType().Name}: {e.Message}";
            }
            finally
            {
                // The same texture may serve several steps; destroy each one once.
                if (scaled != null && scaled != cropped)
                    Object.Destroy(scaled);
                if (cropped != null && cropped != frame)
                    Object.Destroy(cropped);
                if (frame != null)
                    Object.Destroy(frame);
                request.Done = true;
            }
        }

        // Textures count rows from the bottom; crops come with the origin top left.
        private static Texture2D CropTo(Texture2D frame, PixelRect crop)
        {
            int y = frame.height - (crop.Y + crop.Height);
            var pixels = frame.GetPixels(crop.X, y, crop.Width, crop.Height);
            var texture = new Texture2D(crop.Width, crop.Height, TextureFormat.RGB24, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static Texture2D ScaleDown(Texture2D source, int maxPixels)
        {
            int longest = Math.Max(source.width, source.height);
            if (maxPixels <= 0 || longest <= maxPixels)
                return source;
            float factor = (float)maxPixels / longest;
            int width = Math.Max(1, (int)(source.width * factor)), height = Math.Max(1, (int)(source.height * factor));
            var target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                var scaled = new Texture2D(width, height, TextureFormat.RGB24, false);
                scaled.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                scaled.Apply();
                return scaled;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
