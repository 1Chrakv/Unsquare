using System;
using System.Collections.Generic;
using Game;
using Game.Input;
using Unsquare.UI.Editing;

namespace Unsquare.UI.Input
{
    /// <summary>Thin input adapter: pressed hotkeys become <see cref="CageEditSession"/> commands.</summary>
    public partial class CageHotkeySystem : GameSystemBase
    {
        private readonly List<(ProxyAction Action, Action<CageEditSession> Command)> _bindings = new();
        private CageEditorSystem _editor;

        protected override void OnCreate()
        {
            base.OnCreate();
            _editor = World.GetOrCreateSystemManaged<CageEditorSystem>();
        }

        /// <summary>Connects the input actions registered by the settings; call once after RegisterKeyBindings.</summary>
        internal void Bind(UnsquareSettings settings)
        {
            _bindings.Clear();
            foreach (var hotkey in CageHotkeys.All)
            {
                var action = settings.GetAction(hotkey.ActionName);
                if (action == null)
                {
                    Mod.Log.Warn($"Hotkey action {hotkey.ActionName} is not registered.");
                    continue;
                }
                action.shouldBeEnabled = true;
                _bindings.Add((action, hotkey.Command));
            }
        }

        protected override void OnUpdate()
        {
            var session = _editor.Session;
            foreach (var (action, command) in _bindings)
            {
                if (action.WasPressedThisFrame())
                    command(session);
            }
        }
    }
}
