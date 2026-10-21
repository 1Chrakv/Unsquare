namespace Unsquare.Core.Import
{
    /// <summary>Decides per building what the FlexIt import does with its variant and shear.</summary>
    public static class FlexItImportPlanner
    {
        public static FlexItImportAction Decide(FlexItVariant? variant, FlexItShear? shear)
        {
            if (variant.HasValue)
            {
                return variant.Value.Mode switch
                {
                    FlexItMode.Taper => FlexItImportAction.Convert,
                    FlexItMode.Corner => FlexItImportAction.Approximate,
                    _ => FlexItImportAction.ResetToBase,
                };
            }
            return shear.HasValue ? FlexItImportAction.Convert : FlexItImportAction.Skip;
        }
    }
}
