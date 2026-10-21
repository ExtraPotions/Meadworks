# Changelog

## 0.1.14 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Crops grow in the Ashlands again, as in the game and PlantEverything 1.21.4: carrots, onions, turnips, oats, poteitr and kale, and their seed crops. They still need a shield there unless the planter's Farming level reaches HeatToleranceLevel. Barley and flax stay Plains-only, magecap and Jotun puffs Mistlands-only. The Timberwood sapling keeps the game's biomes (Meadows, Mountain, Black Forest, Plains), which PlantEverything 1.21.4 also uses; it does not grow in the Ashlands. Ashvine, the Ashwood sapling, the Ashlands fern, fiddlehead and smoke puff keep the game's own rules.
- New server-owned setting Restrictions: AshlandsCrops (on by default) turns this off; off gives the earlier farmland-only masks. Changes apply right away, to new placements and to crops already planted. Saves and planted crops are unchanged.

## 0.1.13 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- The cultivator no longer gets stuck after a large grid planted with shared materials (Burrow chests). Before, cells beyond Burrow's 24-request queue never finished, so every later placement was refused and the frozen grid outlived a relog. Now at most 8 cells wait on Burrow at once, and any cell that cannot finish right away stops holding your grid and finishes in the background.
- Cells that never got a material hold now close instead of retrying forever. A cell planted before its payment was decided is removed and its materials stay with Burrow. A cell whose payment was decided is never removed or refunded.
- Planting records that can no longer be replayed move to `BepInEx/config/Wildroot/placements/retired` with one warning, instead of being retried on every login. Nothing is deleted.
- Expected recovery messages are now warnings, logged once per grid rather than once per cell.
- Placed resources such as Pickable_Stone show the name of the item they give ("Stone") in the cultivator menu instead of the prefab id.

## 0.1.12 (2026-10-19)

- Simpler in-game settings: the Meadworks menu shows eight main Wildroot settings (on/off, grid planting, rows, columns, snapping, bulk harvest, replanting, Farming perks); Almanac still lists every setting, and 143 rarely changed values (per-plant costs, returns and respawn times, per-species sapling values, per-plant spacing, performance limits, import and managed values, legacy values) are normally edited in the config file. Keys, values and server control are unchanged.
- Farming mode is removed: it only cancelled queued work. F7 and Escape no longer enter or leave it; `Farm.ToggleKey` stays in the config file, unbound and unused. Opening the pause menu or another window still stops grid planting and bulk harvesting in progress.
- The snap (F10) and replant (F6) keys are unbound by default; Grid Snapping Enabled and Harvest Replant are settings rows, and the controller modifier with a right-stick click still toggles snapping. A saved key still at its old default (F10, F6 or F7) is unbound once, with one log line; a key you changed is kept.
- Ripe-resource hover shows one hint line: the harvest keys, plus the replacement crop while replanting is on. Show Hover Key Hints controls the whole line; Show Hover Replant Hint stays in the config file.
- Clicking Wildroot in the Meadworks menu opens its settings; the separate on/off entry appears only when no settings view is available.
- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Deep North (Valheim 1.0.20): the Timberwood sapling (FirTree_big_Sapling) keeps the game's own growth biomes (Meadows, Mountain, Black Forest, Plains) and cold tolerance instead of the PlantEverything farmland-only, cold-intolerant values. Jotun Puffs already matched the game (Mistlands only).
- Settings help: Language, Export English Reference, Grid Enabled and Toggle Key, Farm Toggle Key and Replant Target replace the placeholder descriptions. Keys, defaults and values are unchanged.

## 0.1.11 - batch 4E follow-up (2026-10-19)

- Bulk Planting Batch Size is read from its current setting key, so changing it takes effect.

## 0.1.10 - batch 4E gameplay fixes (2026-10-19)

- Middle-mouse removal of planted crops with the cultivator works again.
- Grid planting places every plant on the previewed cell.
- Plantable icons in the planting menu render at higher resolution.
- Cheats honor the admin "Players may use" switch.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.9 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.8 (2026-10-15)

- First release on Thunderstore.
