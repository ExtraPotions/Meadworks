# Changelog

## 0.5.12 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- German, Russian, Chinese and Korean travel text uses the game's words for the grave and for ships. The cheat section title matches its heading.
- The large-map panel rebuilds its contents when the screen, category, page, language, or which destinations are available changes. It no longer builds that comparison every frame while the map stays still.

## 0.5.11 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- The map panel's settings icon and settings screen are gone; change Wayfarer's settings in Almanac (F8 > Wayfarer). The panel keeps its travel actions, Destinations and gamepad focus.
- The Language override is now marked as normally edited in the config file; it stays in the cfg.
- Settings are tiered for the MeadWorks menu: Enabled, Close map and Tombstone hints are Essential.
- Keys, defaults and saved values are unchanged.

### Settings text

- Setting names and help in English, Spanish and French.
- Spanish: the map buttons and destination tips are now translated, and the stones message names the Sacrificial Stones.
- Keys, defaults and values are unchanged.

## 0.5.10 - batch 4E gameplay fixes (2026-10-19)

- Teleport cheats honor the admin "Players may use" switch.
- Tombstone pin cleanup reports successful retrieval for food top-up.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.


## 0.5.9 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.5.8 (2026-10-17)

- Save language and toggle changes from Wayfarer's dedicated map settings through the shared owning-setting amendment path.
- Report refused edits and saving failures through the existing error handler.

- Remove the matching local death pin after complete tombstone retrieval through direct Take All, inventory Take All or manual transfer. Partial, foreign and ambiguous graves retain their pins. Focused native retrieval/save-reentry and exact-restoration checks passed.

- Yield the map panel and map selection to the active Threshold destination picker; restore the Wayfarer panel when the picker closes.

- Explain map options, destination requests, page controls and category tabs on hover. Existing travel permissions and actions are unchanged.

## 0.5.6 (2026-10-15)

- First release on Thunderstore.
