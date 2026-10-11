using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>Maps a projected anchor to a floating window position on screen.</summary>
    public static class ToolWindowPlacement
    {
        /// <param name="viewport">Viewport point: x, y in 0..1 from the bottom left, z = depth in front of the camera.</param>
        /// <param name="margin">Smallest distance to the screen border, as a share of the screen size.</param>
        /// <param name="position">0..1 from the top left (UI convention), clamped into the margin.</param>
        /// <returns>False when the anchor is behind the camera.</returns>
        public static bool TryPlace(float3 viewport, float margin, out float2 position)
        {
            position = default;
            if (!(viewport.z > 0f) || !math.all(math.isfinite(viewport)))
                return false;
            margin = math.clamp(margin, 0f, 0.5f);
            position = math.clamp(new float2(viewport.x, 1f - viewport.y), margin, 1f - margin);
            return true;
        }
    }
}
