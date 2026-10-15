using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Tells a click from a drag by how far the mouse ray turned since the press, so a click that selects a
    /// handle never moves it by accident.
    /// </summary>
    public static class DragThreshold
    {
        /// <param name="angularSize">Threshold as an angle in radians (see <see cref="ViewScale"/>).</param>
        public static bool IsExceeded(float3 pressDirection, float3 direction, float angularSize)
        {
            float cos = math.dot(math.normalize(pressDirection), math.normalize(direction));
            return math.acos(math.clamp(cos, -1f, 1f)) > angularSize;
        }
    }
}
