## 0.1.27 (2026-10-30)

- Bulk harvest collects every drop before replant seeds are paid for.
- Harvest replants wait out a short shared-payment recovery instead of cancelling it.

## 0.1.26 (2026-10-29)

- Crop growth times apply even with Enable Crop Overrides off; cost, yield, scale and spacing still need the override.
- Pickables already in the world follow respawn-time changes made in game, including turning respawn on or off.

## 0.1.25 (2026-10-29)

- Package files now carry this release's date, so BepInEx reloads updated mods instead of using stale cached plugin details (fixes 'missing dependency Crucible' after updating).

## 0.1.24 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.1.23 (2026-10-28)

- Reduce recovery work for large planting journals by indexing placement probes during each poll while preserving live ownership, permission and lifecycle checks.
- New package icon.

## 0.1.22 (2026-10-26)

- Finished plantings are no longer reopened on every world load. That ends the "saved unfinished plantings" warning at each exit, and a finished paid plant is never replanted for free.
- Finished planting records move to placements/archive when a world loads. The first time, every record is copied to placements/backups first; nothing is deleted, and nothing moves if the backup fails.
- The grid preview rebuilds its ghosts after logging out and back in, which ends the NullReferenceException when the same seed is selected again.

## 0.1.21 - Local candidate

- Hide retired Farm Toggle Key from settings and key-conflict lists while preserving its saved chord and existing migration.

# Changelog

## 0.1.20 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- README: AshlandsCrops needs Restrictions / EnforceBiomesVanilla on, and the Burrow shared-material grid retry is described.

## 0.1.19 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Shared-chest grid cells that become ready after a pick keep planting when their turn returns. Waiting behind other cells no longer consumes the area-readiness timeout or a later unpaid retry. Area waits and unpaid retry counts remain bounded; Escape and unequipping still cancel, and uncertain payments retain their original identity.

## 0.1.18 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Shared-chest grids wait for temporarily unloaded cells before requesting payment, and retry confirmed unpaid cells after short resource or ownership delays. Waits scale with server latency and run in the background without holding the cultivator. Uncertain payments keep their original request until they settle, so late replies cannot charge a cell twice.

- Ordinary crop picks grant seed returns and automatic replants only after the crop owner confirms the harvest. Shared planting counts outstanding jobs toward its admission limit while keeping completed payment and removal evidence. Catalog growth, yield and respawn edits refresh loaded plants without resetting their ages. Gardening HUD text and layout refresh when their inputs change.

- Essential presets group grid size, extra spacing, cultivator effort, Farming progression and crop growth overrides. Member edits select Custom; loading preserves saved values. Host presets apply live without replacing clients' personal preferences. Legacy logging/localization keys and the removed farming-mode key stay in the file, are ignored and are identified once at startup.

- Grid grow-space checks use a prepared collision mask (Default, static_solid, Default_small, piece, piece_nonsolid), the PlantEasily 2.3.0 fix. The game's plant space mask stays empty until its own grow-space method runs, and an empty mask misses nearby plants. Ashlands crops, planting the rest of a grid, and shared-chest planting are unchanged.

## 0.1.17 (2026-10-21)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- A pickable you plant (stones, berries, mushrooms and the like) is marked with the shared world value `meadworks.pickable.planted`. It stays through picks, respawns and saves. Saga uses it to give planted pickables reduced Foraging experience; Wildroot's own behavior is unchanged.

## 0.1.16 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- A shared-material cell that was skipped for a moment is planted after all. Picking up a stone mid-grid makes the game report the area as not ready for a few frames, and the cells being planted right then were canceled. Now a canceled cell is tried once more while you are still planting and its spot is permitted, after waiting up to 5 seconds for the area to be ready; it was never charged. Escape, the pause menu, unequipping and a spot that stays unplaceable still skip it.

## 0.1.15 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- A grid keeps planting when something nearby changes mid-planting, such as picking a stone you just planted. Only the cells that can no longer be placed are skipped, and skipped cells are never charged. Before, the grid could stop as a whole. Your cultivator now counts as long as it is in your right hand, even if another mod rebuilds the item, and wear goes on the cultivator you are holding.
- With Prevent Partial Planting, a whole-grid payment whose cell can no longer be created is returned in full, then the remaining cells are planted one by one. A spot that changed before the grid started is skipped the same way. Escape, the pause menu, unequipping and changing worlds still stop the grid.

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
