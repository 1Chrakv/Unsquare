using System;
using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Converts screen sizes into angular sizes (metres per metre of camera distance), so pick radii
    /// and gizmos keep their on-screen size at any zoom.
    /// </summary>
    public readonly struct ViewScale
    {
        public ViewScale(float verticalFovDegrees, float screenHeightPixels)
        {
            if (!(verticalFovDegrees > 0f && verticalFovDegrees < 180f))
                throw new ArgumentException($"Invalid field of view {verticalFovDegrees}.", nameof(verticalFovDegrees));
            if (!(screenHeightPixels > 0f))
                throw new ArgumentException($"Invalid screen height {screenHeightPixels}.", nameof(screenHeightPixels));
            ViewHeightPerMeter = 2f * math.tan(math.radians(verticalFovDegrees) * 0.5f);
            ScreenHeightPixels = screenHeightPixels;
        }

        /// <summary>Height of the visible area at one metre from the camera.</summary>
        public float ViewHeightPerMeter { get; }

        public float ScreenHeightPixels { get; }

        public float AngularSizeOfPixels(float pixels) => pixels * ViewHeightPerMeter / ScreenHeightPixels;

        /// <param name="fraction">Part of the view height, e.g. 0.1 for a tenth of the screen.</param>
        public float AngularSizeOfViewFraction(float fraction) => fraction * ViewHeightPerMeter;
    }
}
