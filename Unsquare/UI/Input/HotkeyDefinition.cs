using System;
using Unsquare.UI.Editing;

namespace Unsquare.UI.Input
{
    /// <summary>One rebindable key action: its input action name, settings property, label and session command.</summary>
    internal sealed class HotkeyDefinition
    {
        public HotkeyDefinition(string actionName, string propertyName, string label, Action<CageEditSession> command)
        {
            ActionName = actionName;
            PropertyName = propertyName;
            Label = label;
            Command = command;
        }

        public string ActionName { get; }
        public string PropertyName { get; }
        public string Label { get; }
        public Action<CageEditSession> Command { get; }
    }
}
