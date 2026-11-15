using System;
using Unsquare.Core.Naming;

namespace Unsquare.Core.PlacemakerCompatibility
{
    /// <summary>
    /// Recognises the runtime variant prefabs Placemaker bends row houses into ("Placemaker Shape &lt;base&gt; …").
    /// Base and shape come from Placemaker itself, since base names may contain spaces.
    /// </summary>
    public static class PlacemakerVariantName
    {
        public const string Prefix = "Placemaker Shape ";

        /// <summary>Unsquare variants baked on a Placemaker variant (older saves) start with its name but are ours.</summary>
        public static bool IsVariant(string name) =>
            name != null && name.StartsWith(Prefix, StringComparison.Ordinal) && !VariantNaming.IsVariantName(name);
    }
}
