# Changelog

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
