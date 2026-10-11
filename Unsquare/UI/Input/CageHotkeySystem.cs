using System;
using System.Collections.Generic;
using Game;
using Game.Input;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// Thin input adapter: the mode shortcut toggles the Unsquare tool; while it is active, pressed hotkeys
    /// become <see cref="CageEditSession"/> commands. Outside the mode no hotkey does anything.
    /// </summary>
    public partial class CageHotkeySystem : GameSystemBase
    {
        private readonly List<(ProxyAction Action, Action<CageEditSession> Command)> _bindings = new();
        private CageEditorSystem _editor;
        private UnsquareToolSystem _tool;
        private ProxyAction _toggleMode;
        private ProxyAction _exitMode;

        protected override void OnCreate()
        {
            base.OnCreate();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
            _tool = World.GetOrCreateSystemManaged<UnsquareToolSystem>();
        }

        /// <summary>Connects the input actions registered by the settings; call once after RegisterKeyBindings.</summary>
        internal void Bind(UnsquareSettings settings)
        {
            _bindings.Clear();
            _toggleMode = Enable(settings, CageHotkeys.ToggleMode);
            _exitMode = Enable(settings, CageHotkeys.ExitMode);
            foreach (var hotkey in CageHotkeys.All)
            {
                var action = Enable(settings, hotkey.ActionName);
                if (action != null)
                    _bindings.Add((action, hotkey.Command));
            }
        }

        protected override void OnUpdate()
        {
            if (_toggleMode != null && _toggleMode.WasPressedThisFrame())
            {
                _tool.Toggle();
                return;
            }
            if (!_tool.IsActive)
                return;
            if (_exitMode != null && _exitMode.WasPressedThisFrame())
            {
                _tool.Toggle();
                return;
            }
            var session = _editor.Session;
            foreach (var (action, command) in _bindings)
            {
                if (action.WasPressedThisFrame())
                    command(session);
            }
        }

        private static ProxyAction Enable(UnsquareSettings settings, string actionName)
        {
            var action = settings.GetAction(actionName);
            if (action == null)
            {
                Mod.Log.Warn($"Hotkey action {actionName} is not registered.");
                return null;
            }
            action.shouldBeEnabled = true;
            return action;
        }
    }
}
