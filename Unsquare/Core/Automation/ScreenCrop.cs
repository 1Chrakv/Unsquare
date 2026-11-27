using System.Collections.Generic;
using Unity.Mathematics;

namespace Unsquare.Core.Automation
{
    /// <summary>
    /// The screenshot crop around a building: the bounding rectangle of its projected box corners, padded so the
    /// surroundings show, at least a minimum size, and clamped to the screen.
    /// </summary>
    public static class ScreenCrop
    {
        /// <summary>
        /// False when no point lies in front of the camera. <paramref name="points"/> are pixels (top-left origin);
        /// points behind the camera are left out by the caller.
        /// </summary>
        public static bool TryAround(IReadOnlyList<float2> points, int screenWidth, int screenHeight, float padding,
            int minSize, out PixelRect rect)
        {
            rect = default;
            if (points.Count == 0 || screenWidth <= 0 || screenHeight <= 0)
                return false;
            float2 min = points[0], max = points[0];
            for (int i = 1; i < points.Count; i++)
            {
                min = math.min(min, points[i]);
                max = math.max(max, points[i]);
            }
            float2 size = math.max(max - min, minSize);
            float2 centre = (min + max) / 2f;
            float2 half = size * (0.5f + padding);
            float2 screen = new float2(screenWidth, screenHeight);
            float2 low = math.clamp(centre - half, 0f, screen);
            float2 high = math.clamp(centre + half, 0f, screen);
            if (high.x - low.x < 1f || high.y - low.y < 1f)
                return false;
            int x = (int)math.floor(low.x), y = (int)math.floor(low.y);
            rect = new PixelRect(x, y, (int)math.ceil(high.x) - x, (int)math.ceil(high.y) - y);
            return true;
        }
    }
}
