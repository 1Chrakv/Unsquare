using Unity.Mathematics;

namespace Unsquare.Core.Editing
{
    /// <summary>
    /// Running best of a pick by on-screen (angular) distance. Candidates that look equally close
    /// prefer the one nearer to the camera, e.g. the top corner over the one hidden below it.
    /// </summary>
    internal struct ClosestPick
    {
        // Avoids dividing by ~0 for parts right at the camera.
        private const float MinRange = 0.01f;
        private const float TieFraction = 0.1f;

        private readonly float _tolerance;
        private float _angle;
        private float _along;

        public ClosestPick(float tolerance)
        {
            _tolerance = tolerance;
            _angle = float.MaxValue;
            _along = float.MaxValue;
            Index = -1;
        }

        public int Index { get; private set; }
        public bool Found => Index >= 0;

        /// <summary>Distance along the ray to the picked candidate's closest point.</summary>
        public float Along => _along;

        /// <param name="distance">Distance between ray and candidate in metres.</param>
        /// <param name="along">Distance along the ray to the closest point.</param>
        public void Offer(int index, float distance, float along)
        {
            float angle = distance / math.max(along, MinRange);
            if (angle > _tolerance)
                return;
            float slack = _tolerance * TieFraction;
            bool clearlyCloser = angle < _angle - slack;
            bool tieButNearer = angle <= _angle + slack && along < _along;
            if (!clearlyCloser && !tieButNearer)
                return;
            Index = index;
            _angle = angle;
            _along = along;
        }
    }
}
