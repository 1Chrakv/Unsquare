using System;
using Unsquare.Core.Cage;

namespace Unsquare.Core.Automation
{
    /// <summary>Reads handle names as the session prints them (<c>Corner3</c>, <c>Face5</c>, <c>Cage0</c>).</summary>
    public static class HandleName
    {
        public static bool TryParse(string text, out CageHandle handle)
        {
            handle = default;
            if (string.IsNullOrEmpty(text))
                return false;
            int digits = text.Length;
            while (digits > 0 && char.IsDigit(text[digits - 1]))
                digits--;
            if (!Enum.TryParse(text.Substring(0, digits), true, out CageHandleKind kind) ||
                !Enum.IsDefined(typeof(CageHandleKind), kind))
                return false;
            int index = 0;
            if (digits < text.Length && !int.TryParse(text.Substring(digits), out index))
                return false;
            if (index < 0 || index >= CageTopology.CountOf(kind))
                return false;
            handle = kind switch
            {
                CageHandleKind.Corner => CageHandle.Corner(index),
                CageHandleKind.Edge => CageHandle.Edge(index),
                CageHandleKind.Face => CageHandle.Face(index),
                _ => CageHandle.Cage(),
            };
            return true;
        }
    }
}
