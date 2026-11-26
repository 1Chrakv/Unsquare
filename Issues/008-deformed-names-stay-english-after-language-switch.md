# 008 – After a language switch, deformed buildings keep their English names

| | |
|---|---|
| Status | Open, confirmed |
| Found by | In-game test UPGRADE-014, run 10 (2026-11-26, `UnsquareTest-Fixture`), then by hand |
| Severity | Low to medium: wrong language in the info panel and labels for every deformed building |
| Requirement | UPGRADE-014 (old checklist row 125) |

## Summary

Deformed buildings show their base's name through locale aliases (`VariantLocalization`). After switching the game
language from English to German or French, plain buildings show the new language, but deformed ones keep the English
name. Switching back to English shows English again, so the name never breaks; it just doesn't follow the language.

## Steps

1. Load `UnsquareTest-Fixture` (game in English), Unsquare mode on.
2. Deform `carPark` (`ParkingHall03@-1179.2,466.9`): corner 7 by `[1, 0, 0]`; it is now on `ParkingHall03__uq…`.
3. Switch the game language to German (bridge `POST game/locale {"locale": "de-DE"}`), wait 30 frames.
4. Read the names (`GET buildings/{ref}/info`, `NameSystem.GetRenderedLabelName`).

## Actual

| Language | Deformed car park | Plain hospital |
|---|---|---|
| en-US | Multi-Level Parking Building | Hospital |
| de-DE | **Multi-Level Parking Building** | Krankenhaus |
| en-US again | Multi-Level Parking Building | Hospital |
| fr-FR | **Multi-Level Parking Building** | Hôpital |

The same car park while plain shows "Mehrstöckiges Parkhaus" in German.

## Analysis

- `LocalizationManager.SetActiveLocale` loads the new language and then runs `AddMissingEntriesFromFallback`, which
  copies every key the new dictionary lacks from the English fallback dictionary. The variant aliases are such keys,
  so they arrive with their English text before `onActiveDictionaryChanged` fires.
- `VariantLocalization.Add` only adds an alias when `!dictionary.ContainsID(variantKey)`, so it skips them and the
  English text stays.
- Likely fix: in `Add`, set the alias whenever its text differs from the base key's text in the active dictionary,
  instead of only when it is missing.
