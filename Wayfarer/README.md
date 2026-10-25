## Changes in 0.5.13

- Keep landing and map input safe when the teleport service is unavailable.
- Send notices to the log when the native message HUD is unavailable.

# Wayfarer

Version 0.5.13. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

Teleport from the world map: Back to Bed for everyone, plus travel cheats the server switches on, usable by admins unless the server allows others.

## Features

- Open the world map with **M**. By default every player gets **Back to Bed** (you must have claimed a bed in this world).
- Travel cheats, each switched on separately by the server: Sacrificial Stones, Teleport Anywhere (click the map), Boat, Grave (Last Death and your own loaded tombstones) and Player (online players who share their position).
- Boats, graves and players are checked again when you pick them. A distant or unloaded target can be listed but refused until its area is loaded. There is no server-wide boat scan and no hidden-player lookup.
- Safe landing: after a teleport Wayfarer keeps you above solid ground or water. It does not protect you from enemies, lava, weather or other hazards; a water destination can leave you swimming.
- After completely retrieving your own tombstone, Wayfarer removes its uniquely matched death pin from the map. Take All and manual transfers are supported. Partial graves, unrelated pins and ambiguous matches stay marked; dropping contents on the ground is not retrieval. This follows the main Enabled setting.
- Tombstone hints (on by default): your tombstone shows how and when you died, for example "Death by enemy: Troll, Day 42", in each reader's game language using the game's own death texts, so every language the game ships is covered. Only players with Wayfarer or BetterTombstoneHints see the note.
- Map panel at a fixed place beside the map with the travel actions. Change Wayfarer's settings in Almanac (F8 > Wayfarer). Gamepad: the D-pad moves focus, **A** presses, **B** closes the map.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Wayfarer with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Wayfarer` folder from this package into `BepInEx/plugins/`. Restart the game after installing.
- In multiplayer, install Wayfarer on the server and on every client (see Multiplayer).

## Configuration

- Accepted changes in Almanac save by default through the owning setting. Saving failures are reported, and profile updates preserve the saved configuration. Remote server values remain separate from personal preferences.
- Settings file: `BepInEx/config/com.meadworks.wayfarer.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Only a server admin may change the **Server** and **Cheats** sections. The server sends its choices to joining players and checks every teleport request. In single player you are the authority and may use every cheat.

| Section | Setting | Default | Destination |
| --- | --- | --- | --- |
| Server | `AllowTeleport` | `true` | Master switch for every destination |
| Server | `AllowBed` | `true` | Back to Bed (everyone) |
| Cheats | `TeleportToStones` | `false` | Sacrificial Stones |
| Cheats | `TeleportAnywhere` | `false` | Teleport Anywhere |
| Cheats | `TeleportToBoat` | `false` | Boat |
| Cheats | `TeleportToGrave` | `false` | Own Grave / Last Death |
| Cheats | `TeleportToPlayer` | `false` | Online players sharing their position |

- The **Cheats** destinations are Travel cheats. Who may use a switched-on cheat follows Crucible's Mischief policy (`Mischief` section of Crucible's settings, Travel category): Admins by default, or Nobody, Listed players or Everyone. A refused cheat says why instead of failing silently.
- Destinations you may not use are hidden from the map panel.
- Language defaults to Auto (the game's language). Translation overrides load from `BepInEx/config/Meadworks/wayfarer/localization/<Language>.json`.
- Tombstone hints: switch **Tombstone hints** in Almanac (F8 > Wayfarer) or `General.TombstoneHints`.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Wayfarer's travel features turn off for that session, and tombstone hints keep working (they run on your own game). The same holds for a Meadworks server without Wayfarer.
- Players without Meadworks (vanilla clients) cannot join a server running Wayfarer. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Tombstone hints are adapted from BetterTombstoneHints (MIT) and write the same tombstone fields, so notes from either mod show in both. While BetterTombstoneHints is installed, Wayfarer's tombstone hints step aside and say so once; it has no settings to import.
- EasyTeleport: disable it when using Wayfarer so its separate buttons and map-click patch do not overlap. Wayfarer does not change EasyTeleport or import its labels.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Wayfarer

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Tombstone hints adapt BetterTombstoneHints 1.0.0 by JevMods (MIT; licenses/BetterTombstoneHints-MIT.txt, SOURCE_NOTICE.md). No EasyTeleport code, binary, artwork or documentation is included.

Code: GPL-3.0-or-later (LICENSE-CODE.md, licenses/GPL-3.0.txt). Original artwork and documentation: CC BY-NC-SA 4.0 (LICENSE-ASSETS.md, licenses/CC-BY-NC-SA-4.0.txt). Copyright (C) 2026 expDARE. Third-party dependencies keep their own licenses.

Author: expDARE.
