# Changelog

## 1.1.0

- **Building and lot separately.** Each building now has a cage for the building and one for its lot. Edit the
  building, the lot or both (the default, as before); Link makes them equal again. With blending on (off by
  default), the ground beside the building follows its walls and fades out towards the lot edge.
- **Rotate handles.** Tap Ctrl or use the tool window to swap the gizmo arrows for rings and turn an edge or face,
  e.g. twist the top or slope the roof. A fourth pick mode moves or turns the whole cage at once.
- **Level-ups keep the shape.** A deformed growable that levels up gets the same lot outline; leans and roof changes
  scale with its height (option *Keep shape on level-up*).
- **Placemaker:** row houses bent by Placemaker can be converted to Unsquare, one house or the whole row at once.
- **Align to neighbour** also works for the street side and aligns to free-standing footpaths.
- The Unsquare button can be moved to the top left or top right (options).
- Faster reshaping: meshes are deformed in a Burst job and a building is rebuilt right when you release a drag.
- The tool window says why an edit could not be applied.
- The terrain under a turned or skewed lot now slopes with the deformed ground.
- Fences, hedges and lot paths follow the lot edge and the ground better.
- Fixed: copies of deformed buildings (hover previews, eyedropper) could crash the game.

Saves from 1.0.x are migrated automatically; deformed buildings keep their shape.

Known issue (unchanged): forcing an upgrade onto a deformed building (only possible with Anarchy) leads to undefined
behaviour; deleting such an upgrade can crash the game.

## 1.0.1

- Deformed buildings keep their upgrades and can get new ones. On-lot upgrades such as extra car park storeys are
  reshaped together with the building, under one cage.
- Upgrade buildings such as school wings can be picked and reshaped on their own.
- Deformed buildings show their real names in info panels instead of raw keys.
- Fixed: deformed growables lost the zoning under them and could be condemned. Growables deformed with 1.0.0 may
  still be missing their zone; reset them and paint the zone again.

Known issue: forcing an upgrade onto a deformed building (only possible with Anarchy) leads to undefined
behaviour; deleting such an upgrade can crash the game.

## 1.0.0

- First public release.
