using Unity.Mathematics;

namespace Unsquare.Core.Deformation
{
    /// <summary>
    /// Plan footprint of a long, thin prop (fence, hedge, wall segment) in its own space. Rows of such segments form
    /// lines, so they turn with the line's direction (<see cref="RigidDeformer.PlaceLinear"/>), not the average.
    /// </summary>
    public readonly struct LinearFootprint
    {
        /// <summary>Shorter props (benches, bins) keep the usual placement.</summary>
        public const float MinLengthMeters = 3f;

        /// <summary>Length over width from which a prop counts as linear (cars and sheds stay below).</summary>
        public const float MinAspect = 3f;

        private LinearFootprint(float3 axis, float3 centre, float length)
        {
            Axis = axis;
            Centre = centre;
            Length = length;
        }

        /// <summary>Unit x or z: the long axis in the prop's space.</summary>
        public float3 Axis { get; }

        /// <summary>Plan centre in the prop's space, at pivot height (y = 0).</summary>
        public float3 Centre { get; }

        public float Length { get; }

        /// <summary>False for empty bounds and for props that are short or not elongated in plan.</summary>
        public static bool TryCreate(MinMaxBounds bounds, out LinearFootprint footprint)
        {
            footprint = default;
            if (bounds.IsEmpty)
                return false;
            float2 size = bounds.Max.xz - bounds.Min.xz;
            bool alongX = size.x >= size.y;
            float length = alongX ? size.x : size.y, width = math.max(alongX ? size.y : size.x, 1e-3f);
            if (length < MinLengthMeters || length < MinAspect * width)
                return false;
            float2 centre = (bounds.Min.xz + bounds.Max.xz) * 0.5f;
            footprint = new LinearFootprint(alongX ? new float3(1f, 0f, 0f) : new float3(0f, 0f, 1f),
                new float3(centre.x, 0f, centre.y), length);
            return true;
        }
    }
}
