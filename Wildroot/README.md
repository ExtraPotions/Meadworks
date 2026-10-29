# Wildroot

Version 0.1.24. Requires Crucible 0.3.37 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

Gardening for Valheim: plant in grids, harvest in bulk, replant automatically and plant more of the world's flora.

## Features

- The Meadworks menu (F8 or Esc > Meadworks) opens Wildroot's settings: on/off, grid planting, snapping, bulk harvest, replanting, Farming perks and the presets below. Individual numbers, including rows and columns, remain in Almanac. Its own toggle key (`General.ToggleKey`) is unbound by default (PlantEasily used F8, which now opens the Meadworks menu).
- Presets: Grid / SizePreset, Grid / SpacingPreset, Planting / EffortPreset, Farming / ProgressionPreset and Growth.Crops / OverridePreset. Each is Vanilla, MeadWorksDefault, Relaxed or Custom. Choosing one writes that group's values immediately. Editing a member selects Custom. Loading does not replace saved values. Grid size and extra spacing are yours. Cultivator effort, Farming progression and crop overrides belong to the host or server, and joining clients keep their own saved preferences. [Preset values](docs/settings-presets.md).
- Grid planting: plant a whole grid of crops at once, paying the normal planting materials. The grid is at most 10 x 10. Resize it with Right Ctrl + arrow keys (or left bumper + D-pad on a controller) while a plant is selected; a small display shows the grid size and your shortcuts.
- Snapping: new plants line up with the plants next to them. Turn it on or off in the settings, or hold the controller modifier and click the right stick; `Grid.SnapKey` is unbound by default.
- Bulk harvest and automatic replanting. Hold the harvest modifier (`Input.KeyboardHarvestModifierKey`, or the controller modifier) when you pick to harvest everything nearby; a plain pick takes one plant. Turn replanting on or off in the settings (`Harvest.ReplantToggleKey` is unbound by default); you can replant the original crop or the crop you have selected. Without seeds, nothing is replanted.
- An expanded, PlantEverything-style catalog of plantable flora, with server-controlled costs, returns, growth times and restrictions. For crops, the costs, returns and growth overrides apply only when `Growth.Crops.EnableCropOverrides` is on (it is off by default, so crops stay vanilla until a server owner opts in); the other flora follow their catalog entries. Catalog changes apply without a restart.
- Farming skill: the game's own Farming skill grows from planting and picking crops, scaled by `Farming.ExperienceMultiplier`, by how rich the crop is (`Farming.TierScaling`) and a first-pick bonus for each kind of crop (`Farming.DiscoveryMultiplier`). Its skill page lists your perks; death costs Farming levels like any other skill.
- Farming perks, from the level of whoever planted or picked: plants grow up to 40% faster and need up to 40% less room, rain speeds planted crops up by 50% while it falls, picking gives the bonus crop up to 50% of the time (the game's own 25%) and gives a seed back up to 30% of the time.
- High Farming levels: from level 75 your crops grow in the Ashlands without a shield; from level 100 Meadows crops also grow in the Mountain and Deep North.
- Restrictions / AshlandsCrops (host or server, on by default): carrots, onions, turnips, oats, poteitr, kale and their seed crops can grow in the Ashlands, as in the game, while Restrictions / EnforceBiomesVanilla is on (it is on by default). They still need a shield there until the planter's Farming level reaches HeatToleranceLevel. Barley and flax stay Plains-only. Magecap and Jotun puffs stay Mistlands-only. The Timberwood sapling keeps the game's biomes and does not grow in the Ashlands. Ashlands plants keep the game's own rules. Turning AshlandsCrops off removes the Ashlands from those crops, including crops already planted. Turnips and turnip seeds still grow in the swamp and the Mistlands. Saves are unchanged.
- Grid placement checks nearby plants for grow space, so a dense grid does not ignore plants already standing there. Extra crop and sapling spacing stay on SpacingPreset and the extra-spacing settings.
- With Burrow, a shared-material grid keeps planting a cell that is not ready yet, and it tries a cell again when the chest confirms the materials were not taken. That retry runs up to four more times, after a short wait. Time spent queued behind other cells does not use up the ready-wait. A cell that stays unready is skipped after 15 seconds, or longer on a slow connection, and never after more than 45. You can keep using the cultivator while those cells finish in the background. Escape, the pause menu, putting the cultivator away, or losing permission stops the tries. A payment that has not been answered yet keeps its first request, so a late reply cannot charge that cell again.
- Rows and auto replant: planting one crop at a time (a 1 x 1 grid, or grid planting off) plants a row of three from Farming level 25 and five from level 50; hold the alternate place key to plant one.
- Auto replant: from level 50 a picked crop is planted again in the same spot, paid from your inventory (`Farming.RowPlanting` and `Farming.AutoReplant` are your own switches). A larger grid is your own choice and stays as it is.
- Seed crafting: at a workbench, one carrot, turnip, onion, kale or poteitr makes two of its seeds. The recipe shows once you have held the crop (`SeedCrafting.Enabled`).
- Custom extra resources in PlantEverything's format in `BepInEx/config/Wildroot/extra-resources.cfg` (with `Catalog.EnableExtraResources = true`). The file is watched; an invalid file keeps the previous definitions.
- Your own translations can go in `BepInEx/config/Wildroot/localization/<Language>.json`.

## Requirements

- BepInExPack Valheim 5.4.2360
- Jotunn 2.30.2
- Crucible 0.3.37 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Wildroot with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Wildroot` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Wildroot on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.wildroot.cfg`. Edit it with Almanac (the Meadworks settings book), any ConfigurationManager or your mod manager's config editor. Wildroot has no settings window of its own. Changes made to the file outside the game need a restart.
- Grid keys: `Input.IncreaseXKey`, `Input.DecreaseXKey`, `Input.IncreaseYKey`, `Input.DecreaseYKey`, `Input.KeyboardModifierKey` and `Input.GamepadModifierKey`; grid size: `Grid.Rows` and `Grid.Columns` (1 to 10 each; larger or smaller values in the file are clamped).
- In multiplayer the server's values win for server-synced settings (the catalog, growth, costs, yields, the Farming perks and experience settings, seed crafting and the master switch), and admins can lock settings; locked settings show as read-only.
- Cheats: `Gardening / Unrestricted` (plant anywhere and let plants grow without space, sunlight, biome or temperature limits) is off by default. In multiplayer it stays locked unless the server allows it.
- Converter: set `Import.ImportReferenceSettings` to true to import your PlantEasily and PlantEverything settings once. Their files are read unchanged, Wildroot's config is backed up first, and a report is written to `BepInEx/config/Wildroot/import-report.txt`. Conflicts are resolved with `Import.ConflictChoices` and `Import.BindingOverrides`.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Wildroot turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Wildroot. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: with PlantEasily or PlantEverything installed, Wildroot stays off and says so.
- Converter: imports PlantEasily and PlantEverything settings (see Configuration).
- Meadworks: with Burrow installed, planting and replanting can take materials from nearby storage; otherwise your inventory pays. A pickable you plant is marked for Saga's Planted Pickable Foraging XP. Wildroot's own harvest is unchanged.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Wildroot

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Original code uses [GPL-3.0-or-later](LICENSE-CODE.md) (Copyright (C) 2026 expDARE; complete terms in licenses/GPL-3.0.txt). Adopted PlantEverything and PlantEasily portions and the adapted GrindstoneSkills Farming portions retain GPL version 3 terms, and the combined assembly uses GPL version 3. Original artwork/documentation use [CC BY-NC-SA 4.0](LICENSE-ASSETS.md). The place-anywhere and planting input code adapts PlantEverything and PlantEasily by AdvizeGH (GPL version 3); the Farming perks adapt GrindstoneSkills by Gerald Glasgow (GPL-3.0-or-later). Upstream license notices are in THIRD_PARTY_NOTICES/.

Author: expDARE.
