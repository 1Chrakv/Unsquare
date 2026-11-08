using System;
using System.Collections.Generic;
using Game;
using Game.Input;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Unsquare.Core.Editing;
using Unsquare.UI.Editing;
using Unsquare.UI.Tool;

namespace Unsquare.UI.Input
{
    /// <summary>
    /// Thin input adapter: the mode shortcut toggles the Unsquare tool; while it is active, pressed hotkeys
    /// become <see cref="CageEditSession"/> commands and a tap of Ctrl alone switches the gizmo between moving
    /// and rotating. Outside the mode no hotkey does anything.
    /// </summary>
    public partial class CageHotkeySystem : GameSystemBase
    {
        private readonly List<(ProxyAction Action, Action<CageEditSession> Command)> _bindings = new();
        private readonly ModifierTap _ctrlTap = new();
        private CageEditorSystem _editor;
        private UnsquareToolSystem _tool;
        private ProxyAction _toggleMode;
        private bool _loggedTapFailure;

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
            bool tapped = WasCtrlTapped();
            if (!_tool.IsActive)
                return;
            var session = _editor.Session;
            if (tapped)
                session.ToggleGizmoMode();
            foreach (var (action, command) in _bindings)
            {
                if (action.WasPressedThisFrame())
                    command(session);
            }
        }

        // Read straight from the keyboard: the game's bindings cannot bind a modifier on its own.
        private bool WasCtrlTapped()
        {
            try
            {
                var keyboard = Keyboard.current;
                if (keyboard == null)
                    return false;
                bool held = keyboard.ctrlKey.isPressed;
                return _ctrlTap.Update(held, held && IsOtherInput(keyboard), UnityEngine.Time.unscaledTime);
            }
            catch (Exception e)
            {
                if (!_loggedTapFailure)
                    Mod.Log.Warn(e, "Reading Ctrl for the gizmo toggle failed; use the tool window toggle.");
                _loggedTapFailure = true;
                return false;
            }
        }

        private static bool IsOtherInput(Keyboard keyboard)
        {
            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl key = keys[i];
                if (key.isPressed && key.keyCode != Key.LeftCtrl && key.keyCode != Key.RightCtrl)
                    return true;
            }
            var mouse = Mouse.current;
            return mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed ||
                                     mouse.middleButton.isPressed || mouse.scroll.ReadValue().sqrMagnitude > 0f);
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
