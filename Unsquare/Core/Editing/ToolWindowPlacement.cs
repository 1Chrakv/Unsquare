using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Like <see cref="TryPlace"/>, but raised to the highest corner on screen: from an oblique view the far roof
        /// edge projects above the roof centre, and a window there would cover the building.
        /// </summary>
        /// <param name="corners">Viewport points of the cage corners; those behind the camera are ignored.</param>
        public static bool TryPlaceAbove(float3 anchor, IReadOnlyList<float3> corners, float margin,
            out float2 position)
        {
            if (corners == null)
                throw new ArgumentNullException(nameof(corners));
            float top = anchor.y;
            for (int i = 0; i < corners.Count; i++)
            {
                if (corners[i].z > 0f && math.isfinite(corners[i].y))
                    top = math.max(top, corners[i].y);
            }
            return TryPlace(new float3(anchor.x, top, anchor.z), margin, out position);
        }
    }
}
