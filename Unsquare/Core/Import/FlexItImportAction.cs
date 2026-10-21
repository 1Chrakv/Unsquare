namespace Unsquare.Core.Import
{
    public enum FlexItImportAction
    {
        /// <summary>Nothing of FlexIt to import.</summary>
        Skip,

        /// <summary>Taper and/or shear: becomes an Unsquare variant of the base prefab with the same shape.</summary>
        Convert,

        /// <summary>Corner fit: becomes the closest single cage; the fold at the corner is lost.</summary>
        Approximate,

        /// <summary>Arch or bend: back to the base prefab, shear dropped.</summary>
        ResetToBase,
    }
}
