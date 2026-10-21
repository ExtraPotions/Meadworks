# Delve

Version 0.1.11. Requires Crucible 0.3.26 / Crucible.UI 0.3.19. Changes are in [CHANGELOG.md](CHANGELOG.md).

Sets dungeon sizes and brings back loot, resources, the Queen and cleared creatures on timers you choose.

## Features

- Dungeon size: sets how many rooms new dungeons generate; the minimum is capped at 120 because larger values have corrupted worlds.
- Loot chests refill with a fresh roll after a delay (30 in-game days by default). Player-built chests and Hildir's quest chests are never touched. The next opener can be told who opened a chest before, and when.
- Dungeon resources come back: muddy scrap piles, frost cave crystals, black cores, Morkhalla gems, coin piles and black ice, and the Hole's nests and trash piles.
- One-time world objects come back: Mistlands ancient armor, Mountain obsidian, Ashlands flametal spires and Deep North ice.
- The Queen returns to her citadel after her death and a delay without visitors, and the sealed door locks again.
- Kall Fimbulbringer returns asleep to his Deep North boss room after his death and a delay without visitors.
- Creatures of cleared camps and dungeons can respawn (both off by default).
- Every kind has its own on/off switch, delay (in-game days or real hours) and dry run that only reports what would come back. Nothing comes back while a player is near or where a player has built.
- Players are told what comes back and when (at chests, dungeon entrances and on entering a biome); this can be turned off.
- Full dungeon resets: dungeons set to Full in Delve.Dungeons.yml are wiped and generated again with the current room counts once their delay has passed and nobody is inside. A tombstone inside postpones the reset; player buildings inside and player bases block it.
- Admins can bring back felled trees, bushes, mined rocks and ore, and picked stones, flint and branches in generated zones around them by command (never automatic): `delve-surface`, `delve-surface-resources` or `delve-surface-nature` in the chat or console, or Thing's World page.
- Only what the world generator placed comes back; player-built pieces and everything within the base clearance of them, dungeons and locations are never touched, and a zone whose layout no longer matches the generator (for example after a game update) is reported and left alone.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.26 or a later 0.3.x, which includes Crucible.UI 0.3.19 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Delve with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Delve` folder from this package into `BepInEx/plugins/`.
- Install Delve where the world runs: single player, the hosting player, or the dedicated server (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.delve.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager. Delve has no window of its own; its admin page is in Thing.
- The server's values apply to the whole world; admins can lock settings and locked settings show as read-only.
- Cheat: Regenerate Without Waiting is off by default. When on, an admin's regenerate-now command brings a site back at once; players nearby and player buildings still block it. It stays locked unless the server allows it; who may use it follows Crucible's Mischief settings (World category), and Hex is not needed.
- Surface: Allow Surface Reset (on), Surface Base Clearance (50 m around anything a player built, 0-200) and Surface Reset Radius (64 m around the admin, 16-256).
- Converter: imports settings from Max_Dungeon_Rooms, MoreDungeonResets, RestockRegen, Valheim_Admin_Tool and Elite Creatures Reborn after a preview, with a backup.

## Multiplayer

- Server-only: install it on the server; players do not need Delve itself. A copy on a client stays idle on a remote server.
- Players without Meadworks (vanilla clients) can join when the server admin sets Crucible's `Network.AdmissionMode` to `AllowVanilla` and every installed mod allows it (Delve does). The default, `Strict`, refuses clients without Meadworks.

## Compatibility

- Steps aside, module by module, and says so: Max_Dungeon_Rooms turns off dungeon room counts; MoreDungeonResets turns off full dungeon resets; Valheim_Admin_Tool (with automatic resets on) turns off full dungeon resets; RestockRegen turns off chest refills and dungeon resources; Elite Creatures Reborn (with respawning on) turns off creature respawns, and with dungeon loot respawning on, chest refills.
- Converter: imports the settings of all five mods above (see Configuration).
- Optional Meadworks integrations: Thing shows Delve's admin page and its dungeon and resource clocks.

## Notes

- Gamepad: not applicable; Delve runs on the server and has no player window.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): MoreDungeonResets (Loathe), Max_Dungeon_Rooms (Digitalroot).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Delve

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Chest refill, history and spot memory are adapted from RestockRegen by liekos47 (MIT); the spawner respawn rules from Elite Creatures Reborn by Gerald Glasgow (GPL-3.0-or-later). Room counts follow the behavior of Max_Dungeon_Rooms by Digitalroot (clean room, no code used); full dungeon resets follow MoreDungeonResets by Loathe and automatic resets follow Valheim_Admin_Tool by QoLTeam (behavior references, no code used).

Author: expDARE.
