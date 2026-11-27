# 010 – A Ctrl tap never switches the gizmo between Move and Rotate

| | |
|---|---|
| Status | Open, confirmed (cause found in the code) |
| Found by | Phase 3 input spike (2026-11-27, `UnsquareTest-Fixture`, Debug build of `feature/ingame-input`) |
| Severity | Medium: the advertised shortcut does nothing; the tool-window toggle still works |
| Requirement | GIZMO-003 (old checklist rows 145, 146, 152, 178) |

## Summary

Tapping Ctrl in Unsquare mode with a handle selected leaves the gizmo in Move mode. Every frame with Ctrl held throws
a `NullReferenceException` in `CageHotkeySystem`; the first one is logged as a warning, later ones silently.

## Steps

1. Unsquare mode on, select `plainHouse`, select its top face (`Face3`).
2. Tap Left Ctrl (press and release within 0.5 s, no other key).

Seen with the bridge's virtual keyboard and with key events on the real keyboard (`input/key`, `via` virtual and
current, hold 1 to 10 frames).

## Expected

`[Edit] Gizmo mode Rotate`; the gizmo shows rings (GIZMO-003).

## Actual

The gizmo stays in Move mode. `Unsquare.Mod.log`:

```
[WARN]  Reading Ctrl for the gizmo toggle failed; use the tool window toggle.
NullReferenceException: Object reference not set to an instance of an object
  at Unsquare.UI.Input.CageHotkeySystem.IsOtherInput (UnityEngine.InputSystem.Keyboard keyboard) [...] CageHotkeySystem.cs:93
  at Unsquare.UI.Input.CageHotkeySystem.WasCtrlTapped () [...] CageHotkeySystem.cs:76
```

## Analysis

`IsOtherInput` walks `keyboard.allKeys` and reads `key.isPressed` on every entry. `allKeys` has null entries for key
codes the keyboard layout doesn't have (the bridge's own key listing hit the same nulls). The exception is caught in
`WasCtrlTapped`, which then returns false for every frame, so no tap is ever reported. It only runs while Ctrl is
held (`held && IsOtherInput(…)`), so nothing else is affected.

## Next steps

- Skip null entries in `IsOtherInput` (`if (key != null && key.isPressed …)`).
- Rerun GIZMO-003 (`specs/input/`) once the input specs exist; it then covers the tap and its exceptions.
