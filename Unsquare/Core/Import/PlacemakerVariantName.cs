using System;

namespace Unsquare.Core.Import
{
    /// <summary>
    /// Recognises the runtime variant prefabs Placemaker bends row houses into ("Placemaker Shape &lt;base&gt; …").
    /// Only a fallback: the base and the shape come from Placemaker itself, since base names may contain spaces.
    /// </summary>
    public static class PlacemakerVariantName
    {
        public const string Prefix = "Placemaker Shape ";

        public static bool IsVariant(string name) =>
            name != null && name.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
