using System;
using System.Globalization;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// Parses FlexIt variant prefab names: "&lt;Base&gt;__taper&lt;A&gt;_&lt;B&gt;[_&lt;C&gt;_&lt;D&gt;]", or "__corner", "__arch",
    /// "__bend" with two values. Saves reference variants by this name, so no FlexIt data files are needed.
    /// </summary>
    public static class FlexItVariantName
    {
        // Same precedence as FlexIt: a base name may itself contain a separator checked later.
        private static readonly (string Separator, FlexItMode Mode)[] Separators =
        {
            ("__corner", FlexItMode.Corner),
            ("__arch", FlexItMode.Arch),
            ("__taper", FlexItMode.Taper),
            ("__bend", FlexItMode.Bend),
        };

        public static bool TryParse(string name, out FlexItVariant variant)
        {
            variant = default;
            if (string.IsNullOrEmpty(name))
                return false;
            foreach (var (separator, mode) in Separators)
            {
                int at = name.LastIndexOf(separator, StringComparison.Ordinal);
                if (at < 0)
                    continue;
                if (at == 0 || !TryParseValues(name.Substring(at + separator.Length), mode, out var v))
                    return false;
                variant = new FlexItVariant(name.Substring(0, at), mode, v[0], v[1], v[2], v[3]);
                return true;
            }
            return false;
        }

        public static bool IsVariantName(string name) => TryParse(name, out _);

        private static bool TryParseValues(string text, FlexItMode mode, out int[] values)
        {
            values = new int[4];
            string[] parts = text.Split('_');
            if (parts.Length != 2 && !(parts.Length == 4 && mode == FlexItMode.Taper))
                return false;
            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture,
                        out values[i]))
                    return false;
            }
            return true;
        }
    }
}
