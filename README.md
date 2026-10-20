# Unsquare

A [Cities: Skylines II](https://www.paradoxinteractive.com/games/cities-skylines-ii) code mod that lets you freely
deform individual buildings. Pick a building and drag the corners, edges or faces of its lot box: the building is
reshaped by a trilinear cage deformation, so it can be tapered, skewed, twisted, raised or leaned to fit odd plots,
angled streets and slopes.

> [!IMPORTANT]
> **Deformation is cosmetic only.** It changes how a building and its lot look, not what the game simulates: lot
> size, zone cells and the building's simulation (residents, jobs, level, upkeep) stay those of the original
> rectangular building.

## Features

- **Cage deformation of the lot box.** The cage spans the whole lot (lot rectangle from the ground to the roof), with
  8 corners, 12 edges and 6 faces as handles. All LODs of the building are deformed.
- **The lot follows.** Props, sub-buildings, ground areas (paving, grass), lanes and paths on the lot move with the
  cage. Props stay rigid but follow position and rotation, and turn with the nearest facade; ground props stay upright.
  Path ends at the road slide along it and stay connected.
- **Terrain lift.** When the bottom of the cage is raised or lowered, the ground under the lot follows. It can be
  switched off per building to keep a flat pad.
- **Fit to ground.** Moves the bottom corners onto the original terrain: the back follows the slope and the street
  side goes to road level, or all four corners follow the terrain.
- **Undo / redo** of every cage edit, across all buildings.
- **Saved with the city.** Deformed buildings are baked into variant prefabs and restored when a save is loaded.
  Identical deformations share their baked assets.

## Limitations

- **Terrain follows the original lot box.** The game flattens and blends the ground over the rectangular lot
  footprint; Unsquare only tilts that flat area and the lot edges. A lot pushed outside its rectangle gets no ground
  shaping there, and diagonal creases come from the game's blending.
- **The lot itself stays rectangular.** Lot size, zone cells and the plot outline are not deformed; pulling the lot
  outside its plot only shows a warning (magenta cage) and may overlap neighbours.
- **Props are placed rigidly.** They move and turn with the cage but their meshes are not bent, so large props on
  strongly bent lots can look stiff or clip.
- **Effects** (lights, smoke and similar) are not moved with the deformation yet.
- **Strong twists** drop the coarsest LOD, which can't follow the bend; very distant buildings may pop.
- **Only buildings with a lot** can be deformed. A handle can't be pushed through the opposite side, and extreme
  moves are clamped.
- **Saves depend on the mod.** Deformed buildings are variant prefabs; without Unsquare they can't be restored.
  While in early development, an update may also discard older deformations.

## Usage

1. Turn on **Unsquare mode** with the button next to the underground toggle, or press **Alt+U**.
2. Click a building. It becomes the edit target and its cage appears. Hovering other buildings previews their cage.
3. Click a corner, edge or face (depending on the pick mode) to select it. A gizmo appears on the selection:
   - drag an arrow (X red, Y green, Z blue) to move along that axis,
   - drag the yellow square, or press on a handle and drag, to move horizontally (heights are kept).
4. Right-click steps back: it cancels a drag, then clears the handle, then the building. Esc, Alt+U or the button
   leave the mode.

While a handle is selected the building is locked; clicks on other buildings do nothing until you step back.

### Tool window

While a building is selected, a small window floats above its roof. It switches the pick mode (corners, edges,
faces), resets the selected handle or the whole building, toggles the terrain lift and fits the building to the
ground.

### Hotkeys

Defaults; all can be rebound in Options → Unsquare. Except Alt+U, they only work in Unsquare mode.

| Action | Key |
|---|---|
| Unsquare mode on / off | Alt+U |
| Leave Unsquare mode | Esc |
| Cycle pick mode (edges → faces → corners) | Alt+W |
| Pick corners / edges / faces | Alt+1 / Alt+2 / Alt+3 |
| Select previous / next handle | Alt+Q / Alt+E |
| Move along X (width) | Alt+← / Alt+→ |
| Move along Z (+Z = street side) | Alt+↑ / Alt+↓ |
| Move up / down | Alt+PageUp / Alt+PageDown |
| Cycle step size (0.1 / 0.5 / 2 m) | Alt+S |
| Reset selected handle | Alt+R |
| Reset building / reset all buildings | Alt+Backspace / Alt+Shift+Delete |
| Terrain lift on / off | Alt+T |
| Fit to ground (back to terrain, street side to road) / all corners to terrain | Alt+G / Alt+Shift+G |
| Undo / redo | Ctrl+Z / Ctrl+Y |
| Write the cage state to the log | Alt+L |

### Overlay

Dashed lines show the original box. The cage colour shows its state: white undeformed, yellow pending, green applied,
red failed, magenta when the lot outline leaves its plot (the edit still applies). The selected handle is orange,
the hovered one cyan.

### Options

Options → Unsquare → Advanced holds the tunables: cage limits, which attachments follow the cage, facade alignment,
nudge step sizes, picking and gizmo sizes, and overlay style.

### Before uninstalling

Saves with deformed buildings need the mod to restore them. Reset deformed buildings (Alt+Shift+Delete resets all)
and save before removing the mod.

## Building from source

Requirements:

- The Cities: Skylines II modding toolchain (installed from the game's Options → Modding). It sets the
  `CSII_TOOLPATH` (imports `Mod.props` / `Mod.targets`), `CSII_MANAGEDPATH` (game assemblies) and
  `CSII_USERDATAPATH` environment variables.
- .NET SDK (the mod targets .NET Framework 4.8, C# 9).
- Node.js 18 or newer, for the React UI module in `UiModule/`.

```bash
dotnet build Unsquare.sln
```

```bash
dotnet test Unsquare.Tests
```

The build deploys the mod to the game's `Mods\Unsquare` folder and then builds the UI module into it (`npm run build`
in `UiModule/`). Close the game first: it locks the mod DLL and the deploy replaces the whole folder.
`Unsquare.Tests` runs without the game: it compiles only the pure logic in `Unsquare/Core/`.

More detail: [`Unsquare/HowTo.md`](Unsquare/HowTo.md) (deformation pipeline) and
[`Unsquare/Testing.md`](Unsquare/Testing.md) (in-game test protocol).

## Credits

Inspired by [Flex It](https://mods.paradoxplaza.com/mods/151744/Windows) by Root Of Business. Unsquare is an
independent implementation and contains no code from Flex It.

## Third-party notes

- **Newtonsoft.Json** (MIT, © James Newton-King) is used for the variant manifest. It is referenced from the game
  installation and not shipped with the mod.
- **React** and the `cs2/*` UI APIs are provided by the game at runtime and not bundled.
- **UI module template:** `UiModule/` was created from the official Cities: Skylines II UI mod template
  (`create-csii-ui-mod`); the build configuration, `tools/` and the `types/` declarations come from it.
- The deployed mod (`Mods\Unsquare`) contains only Unsquare's own files: `Unsquare.dll`, its Burst-compiled native
  libraries, and the UI module (`Unsquare.mjs`, `Unsquare.css`, `images/`). No third-party DLLs are shipped.

## Disclaimer

Unsquare is an unofficial fan-made mod. It is not affiliated with, endorsed by or supported by Colossal Order or
Paradox Interactive. Cities: Skylines II is a trademark of its respective owners. This repository contains no game
code or game assets; building it requires your own copy of the game.

## License

Copyright (C) 2026 Chrakv

Unsquare is free software: you can redistribute it and/or modify it under the terms of the
[GNU General Public License v3.0](LICENSE) or (at your option) any later version.

**Additional permission under GNU GPL version 3 section 7:** if you modify this program, or any covered work, by
linking or combining it with Cities: Skylines II (or a modified version of it), including the game's assemblies, the
Unity engine and other mods loaded by the game, containing parts covered by the terms of their respective licenses,
the licensors of this program grant you additional permission to convey the resulting work.
