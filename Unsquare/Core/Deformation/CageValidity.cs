namespace Unsquare.Core.Deformation
{
    public enum CageValidity
    {
        Valid,

        /// <summary>A corner offset exceeds <see cref="CageValidator.OffsetLimit"/>.</summary>
        OutOfRange,

        /// <summary>The cage folds over or collapses somewhere (Jacobian determinant too small).</summary>
        Inverted,

        /// <summary>Both cages are fine, but the ground blending between them folds over.</summary>
        BlendInverted,
    }
}
