namespace Unsquare.Core.Import
{
    /// <summary>Decides per building what the FlexIt import does with its variant and shear.</summary>
    public static class FlexItImportPlanner
    {
        public static FlexItImportAction Decide(FlexItVariant? variant, FlexItShear? shear)
        {
            if (variant.HasValue && variant.Value.IsCurved)
                return FlexItImportAction.ResetToBase;
            return variant.HasValue || shear.HasValue ? FlexItImportAction.Convert : FlexItImportAction.Skip;
        }
    }
}
