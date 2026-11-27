#if UNSQUARE_AUTOMATION
using System;
using System.Collections.Generic;
using Game.Input;
using Newtonsoft.Json.Linq;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using InputManager = Game.Input.InputManager;

namespace Unsquare.Automation
{
    /// <summary>
    /// Mouse and keyboard input for tests, fed into Unity's input system so the game handles it like the player's:
    /// a virtual mouse and keyboard of their own (default), or events on the current real devices. Positions are
    /// pixels with the origin top left.
    /// </summary>
    internal sealed class SyntheticInput : IDisposable
    {
        public const string Virtual = "virtual";
        public const string Current = "current";

        private Mouse _virtualMouse;
        private Keyboard _virtualKeyboard;
        private Vector2 _position;
        private bool _left, _right;

        /// <summary>Which devices get the events: <see cref="Virtual"/> or <see cref="Current"/>.</summary>
        public string Via { get; set; } = Virtual;

        /// <summary>Tells the game it has focus before each event (its raycast only runs with focus).</summary>
        public bool ForceFocus { get; set; }

        public float2 Position => new(_position.x, Screen.height - _position.y);

        private Mouse TargetMouse =>
            Via == Current ? Mouse.current : _virtualMouse ??= InputSystem.AddDevice<Mouse>("UnsquareTestMouse");

        private Keyboard TargetKeyboard =>
            Via == Current ? Keyboard.current : _virtualKeyboard ??= InputSystem.AddDevice<Keyboard>("UnsquareTestKeyboard");

        public void Pointer(float2 topLeft, bool? left = null, bool? right = null)
        {
            var position = new Vector2(topLeft.x, Screen.height - topLeft.y);
            var state = new MouseState { position = position, delta = position - _position };
            _position = position;
            _left = left ?? _left;
            _right = right ?? _right;
            state = state.WithButton(MouseButton.Left, _left).WithButton(MouseButton.Right, _right);
            Send(TargetMouse, state);
        }

        public void Buttons(bool? left = null, bool? right = null) => Pointer(Position, left, right);

        public void Keys(IReadOnlyCollection<Key> pressed)
        {
            var keys = new Key[pressed.Count];
            int i = 0;
            foreach (var key in pressed)
                keys[i++] = key;
            Send(TargetKeyboard, new KeyboardState(keys));
        }

        private void Send<TState>(InputDevice device, TState state) where TState : struct, IInputStateTypeInfo
        {
            if (device == null)
                throw new InvalidOperationException($"No {(Via == Current ? "current" : "virtual")} device.");
            if (ForceFocus)
                InputManager.instance?.OnFocusChanged(true);
            InputSystem.QueueStateEvent(device, state);
            device.MakeCurrent();
        }

        /// <summary>Releases everything and removes the virtual devices, so the player's devices take over again.</summary>
        public void Reset()
        {
            if (_virtualMouse != null || Via == Current)
                Buttons(false, false);
            if (_virtualKeyboard != null || Via == Current)
                Keys(Array.Empty<Key>());
            InputSystem.Update();
            if (_virtualMouse != null)
                InputSystem.RemoveDevice(_virtualMouse);
            if (_virtualKeyboard != null)
                InputSystem.RemoveDevice(_virtualKeyboard);
            _virtualMouse = null;
            _virtualKeyboard = null;
            _left = _right = false;
        }

        public void Dispose() => Reset();

        /// <summary>
        /// The keys a binding presses now: its key plus its modifiers (left variants), after any rebinding. False if
        /// it has no keyboard binding.
        /// </summary>
        public bool TryKeysOf(ProxyAction action, out List<Key> keys, out string text)
        {
            keys = new List<Key>();
            text = null;
            foreach (var binding in action.bindings)
            {
                if (!binding.isKeyboard || string.IsNullOrEmpty(binding.path))
                    continue;
                foreach (var modifier in binding.modifiers)
                    if (TryKey(modifier.m_Path, out var modifierKey))
                        keys.Add(modifierKey);
                if (!TryKey(binding.path, out var key))
                    continue;
                keys.Add(key);
                text = binding.path;
                return true;
            }
            return false;
        }

        /// <summary>A key for a control path like <c>&lt;Keyboard&gt;/z</c> or <c>&lt;Keyboard&gt;/shift</c>.</summary>
        public static bool TryKey(string path, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(path))
                return false;
            var name = path.Substring(path.LastIndexOf('/') + 1);
            switch (name.ToLowerInvariant())
            {
                case "shift": key = Key.LeftShift; return true;
                case "ctrl": key = Key.LeftCtrl; return true;
                case "alt": key = Key.LeftAlt; return true;
            }
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.TryGetChildControl<KeyControl>(name) is { } control)
            {
                key = control.keyCode;
                return true;
            }
            return Enum.TryParse(name, true, out key) && key != Key.None;
        }

        public JObject Describe()
        {
            var devices = new JArray();
            foreach (var device in InputSystem.devices)
                devices.Add(new JObject
                {
                    ["name"] = device.name,
                    ["enabled"] = device.enabled,
                    ["canRunInBackground"] = device.canRunInBackground,
                });
            var manager = InputManager.instance;
            return new JObject
            {
                ["via"] = Via,
                ["forceFocus"] = ForceFocus,
                ["applicationFocused"] = Application.isFocused,
                ["runInBackground"] = Application.runInBackground,
                ["backgroundBehavior"] = InputSystem.settings.backgroundBehavior.ToString(),
                ["currentMouse"] = Mouse.current?.name,
                ["currentKeyboard"] = Keyboard.current?.name,
                ["mousePosition"] = manager != null ? BridgeContext.Json((float3)manager.mousePosition) : null,
                ["mouseOnScreen"] = manager?.mouseOnScreen,
                ["mouseOverUI"] = manager?.mouseOverUI,
                ["controlOverWorld"] = manager?.controlOverWorld,
                ["controlScheme"] = manager?.activeControlScheme.ToString(),
                ["pointer"] = new JArray(Position.x, Position.y),
                ["devices"] = devices,
            };
        }
    }
}
#endif
