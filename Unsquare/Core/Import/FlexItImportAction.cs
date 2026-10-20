namespace Unsquare.Core.Import
{
    public enum FlexItImportAction
    {
        /// <summary>Nothing of FlexIt to import.</summary>
        Skip,

        /// <summary>Taper and/or shear: becomes an Unsquare variant of the base prefab.</summary>
        Convert,

        /// <summary>Curved variant: back to the base prefab, shear dropped.</summary>
        ResetToBase,
    }
}
