using Unity.Mathematics;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// FlexIt's render-only shear in building space: x leans with depth around <see cref="PivotZ"/>, or with
    /// <see cref="LeanFront"/> z leans with width. FlexIt applies it on top of the building's (variant) mesh.
    /// </summary>
    public readonly struct FlexItShear
    {
        public FlexItShear(float degrees, bool leanFront, float pivotZ)
        {
            Degrees = degrees;
            LeanFront = leanFront;
            PivotZ = pivotZ;
        }

        public float Degrees { get; }
        public bool LeanFront { get; }
        public float PivotZ { get; }

        public float2 Map(float2 xz)
        {
            float tan = math.tan(math.radians(Degrees));
            return LeanFront
                ? new float2(xz.x, xz.y + tan * xz.x)
                : new float2(xz.x + tan * (xz.y - PivotZ), xz.y);
        }

        public override string ToString() => $"{Degrees:0.#} deg{(LeanFront ? " lean front" : "")}";
    }
}
