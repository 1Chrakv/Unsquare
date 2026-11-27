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
using UnityEngine.InputSystem.Users;
using InputManager = Game.Input.InputManager;

namespace Unsquare.Automation
{
    /// <summary>
    /// Mouse and keyboard input for tests, fed into Unity's input system so the game handles it like the player's:
    /// a virtual mouse and keyboard of their own (default), or events on the current real devices. Positions are
    /// pixels with the origin top left.
    /// </summary>
    internal sealed unsafe class SyntheticInput : IDisposable
    {
        public const string Virtual = "virtual";
        public const string Current = "current";
        private const string DevicePrefix = "UnsquareTest";

        public SyntheticInput() => InputSystem.onDeviceCommand += RunInBackground;

        private Mouse _virtualMouse;
        private Keyboard _virtualKeyboard;
        private Vector2 _position;
        private bool _left, _right, _middle;

        /// <summary>Which devices get the events: <see cref="Virtual"/> or <see cref="Current"/>.</summary>
        public string Via { get; set; } = Virtual;

        /// <summary>Tells the game it has focus before each event (its raycast only runs with focus).</summary>
        public bool ForceFocus { get; set; }

        public float2 Position => new(_position.x, Screen.height - _position.y);

        private Mouse TargetMouse =>
            Via == Current ? Mouse.current : _virtualMouse ??= Paired(InputSystem.AddDevice<Mouse>(DevicePrefix + "Mouse"));

        private Keyboard TargetKeyboard =>
            Via == Current ? Keyboard.current : _virtualKeyboard ??= Paired(InputSystem.AddDevice<Keyboard>(DevicePrefix + "Keyboard"));

        /// <summary>
        /// Lets our devices run while the game is in the background (the player works in another window); Unity
        /// otherwise disables them and drops their events. The player's keyboard stays off for the game.
        /// </summary>
        private static long? RunInBackground(InputDevice device, InputDeviceCommand* command)
        {
            if (command->type != QueryCanRunInBackground.Type || device?.name == null ||
                !device.name.StartsWith(DevicePrefix, StringComparison.Ordinal))
                return null;
            ((QueryCanRunInBackground*)command)->canRunInBackground = true;
            return InputDeviceCommand.GenericSuccess;
        }

        /// <summary>
        /// Pairs a device with the game's input user in place of the player's device of that kind, as the game does when
        /// a device is first used. Otherwise that first use only pairs it: the press is lost and the UI keeps
        /// following the player's mouse.
        /// </summary>
        private static T Paired<T>(T device) where T : InputDevice
        {
            var manager = InputManager.instance;
            if (manager == null || !manager.inputUser.valid)
                return device;
            var user = manager.inputUser;
            foreach (var paired in user.pairedDevices.ToArray())
                if (paired is T && paired != device)
                    user.UnpairDevice(paired);
            InputUser.PerformPairingWithDevice(device, user);
            return device;
        }

        private static bool IsPaired(InputDevice device)
        {
            var manager = InputManager.instance;
            if (manager == null || !manager.inputUser.valid)
                return true;
            foreach (var paired in manager.inputUser.pairedDevices)
                if (paired == device)
                    return true;
            return false;
        }

        // Gives the player's mouse and keyboard back to the game once ours are gone.
        private static void PairPlayerDevices()
        {
            var manager = InputManager.instance;
            if (manager == null || !manager.inputUser.valid)
                return;
            var user = manager.inputUser;
            bool hasMouse = false, hasKeyboard = false;
            foreach (var paired in user.pairedDevices)
            {
                hasMouse |= paired is Mouse;
                hasKeyboard |= paired is Keyboard;
            }
            foreach (var device in InputSystem.devices)
            {
                if (device.name.StartsWith(DevicePrefix, StringComparison.Ordinal))
                    continue;
                if (!hasMouse && device is Mouse)
                {
                    InputUser.PerformPairingWithDevice(device, user);
                    hasMouse = true;
                }
                else if (!hasKeyboard && device is Keyboard)
                {
                    InputUser.PerformPairingWithDevice(device, user);
                    hasKeyboard = true;
                }
            }
        }

        public void Pointer(float2 topLeft, bool? left = null, bool? right = null, bool? middle = null)
        {
            var position = new Vector2(topLeft.x, Screen.height - topLeft.y);
            var state = new MouseState { position = position, delta = position - _position };
            _position = position;
            _left = left ?? _left;
            _right = right ?? _right;
            _middle = middle ?? _middle;
            state = state.WithButton(MouseButton.Left, _left).WithButton(MouseButton.Right, _right)
                .WithButton(MouseButton.Middle, _middle);
            Send(TargetMouse, state);
        }

        public void Buttons(bool? left = null, bool? right = null, bool? middle = null) =>
            Pointer(Position, left, right, middle);

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
            // The player's mouse also runs in the background; moving it takes the pairing back from ours.
            if (Via == Virtual && !IsPaired(device))
                Paired(device);
            InputSystem.QueueStateEvent(device, state);
            device.MakeCurrent();
        }

        /// <summary>Releases every button and key of the devices we drive (seen by the game in the next frame).</summary>
        public void Release()
        {
            if (_virtualMouse != null || Via == Current)
                Buttons(false, false, false);
            if (_virtualKeyboard != null || Via == Current)
                Keys(Array.Empty<Key>());
            _left = _right = _middle = false;
        }

        /// <summary>
        /// Gives the pairing back to the player's mouse and keyboard and the focus flag back to the truth; call a frame
        /// after <see cref="Release"/>. Our devices stay: removing a paired device shows "Controller disconnected".
        /// </summary>
        public void HandBack()
        {
            var manager = InputManager.instance;
            if (manager != null && manager.inputUser.valid)
            {
                var user = manager.inputUser;
                foreach (var paired in user.pairedDevices.ToArray())
                    if (paired == _virtualMouse || paired == _virtualKeyboard)
                        user.UnpairDevice(paired);
                PairPlayerDevices();
            }
            if (ForceFocus)
                manager?.OnFocusChanged(Application.isFocused);
        }

        public void Dispose()
        {
            Release();
            InputSystem.Update();
            HandBack();
            if (_virtualMouse != null)
                InputSystem.RemoveDevice(_virtualMouse);
            if (_virtualKeyboard != null)
                InputSystem.RemoveDevice(_virtualKeyboard);
            InputSystem.onDeviceCommand -= RunInBackground;
        }

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

        private static JArray PairedNames(InputManager manager)
        {
            var names = new JArray();
            if (manager != null && manager.inputUser.valid)
                foreach (var device in manager.inputUser.pairedDevices)
                    names.Add(device.name);
            return names;
        }

        private static JArray PressedKeys(Keyboard keyboard)
        {
            var pressed = new JArray();
            if (keyboard != null)
                // allKeys has gaps (null entries) for key codes this keyboard layout doesn't have.
                foreach (var key in keyboard.allKeys)
                    if (key != null && key.isPressed)
                        pressed.Add(key.keyCode.ToString());
            return pressed;
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
                ["paired"] = PairedNames(manager),
                ["pointer"] = new JArray(Position.x, Position.y),
                ["keysDown"] = PressedKeys(Keyboard.current),
                ["ctrlDown"] = Keyboard.current?.ctrlKey.isPressed,
                ["buttonsDown"] = Mouse.current == null ? null : new JArray(Mouse.current.leftButton.isPressed,
                    Mouse.current.rightButton.isPressed, Mouse.current.middleButton.isPressed),
                ["devices"] = devices,
            };
        }
    }
}
#endif
