## Changes in 0.1.17
- New package icon.

# Magpie

Version 0.1.17. Requires Crucible 0.3.36 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

Items within range glide to you and are picked up, nearby ground stacks merge, and drops float; an optional loot multiplier is a server-controlled cheat.

## Features

- Pickup: items within your pickup range glide to you and are taken automatically. Range (3.5 m by default, capped by the server's Max Pickup Range), magnet speed and an ignore list are settings.
- Grouped pickup messages (one line per item kind with the summed amount) and no pickup particles or sound, both optional.
- Optional pickup toggle key and gamepad chord (unbound by default), alongside the game's own auto pickup key.
- Merge: identical items lying near each other on the ground merge into one stack after they settle.
- Float: dropped items float on water and tar instead of sinking. Items you list can keep sinking.
- Loot multiplier (cheat, off by default): world loot you pick up from creatures, destructibles, rocks, trees, pickables and loot spawners can be multiplied. Player drops, chest and tombstone spills, refunds and station output never count.
- Pickup, Merge, Float and the loot multiplier each have their own switch.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.36 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Magpie with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Magpie` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Magpie on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.magpie.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings (module switches, merging, floating, Max Pickup Range). Your pickup range, speed, messages and keys stay personal unless the server lists them in Locked Player Settings. Locked settings show as read-only.
- Cheats: Loot Multiplier is 1 (off) by default. In multiplayer the server's admins can use it (the server's value applies); for everyone else it reads 1 and a note says why. Crucible's Mischief settings do not change this. Extra items from the loot multiplier keep the cheated flag of the item you picked up; Magpie does not add a cheated flag to clean extras. While Pickup, Merge or the loot multiplier is on, Magpie keeps Valheim's cheated flag (m_cheated) on ground items through saves and reloads; servers running CheatTagEnforcement remove items that carry it.
- Converter: Magpie can import your SwiftPickup and FloatingItems (current and legacy) settings after a preview, with a backup first (single player or local settings only). The source files are left unchanged.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Merging runs on single player, on a host and on the server. On a dedicated server, drops owned by different players' games do not merge (item safety); single player and the host merge as described above.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Magpie turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Magpie. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: with SwiftPickup installed, Magpie's pickup and loot multiplier turn off, and so does merging while SwiftPickup's merge option is on. With FloatingItems installed (current or legacy version), Magpie's floating turns off. Each yielding module logs one notice.
- Converter imports: SwiftPickup and FloatingItems settings (see Configuration).
- With Inscribe: the loot multiplier also applies once to stackable runes from creatures; gear is not multiplied.
- With Wildblood: when Wildblood's Loot module already multiplies creature drops (above 1), Magpie's loot multiplier skips those drops so they are not multiplied twice.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): SwiftPickup (SoulEmper0r).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Magpie

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Pickup adapts OpenKeep CameraPickup by Gerald Glasgow (GPL-3.0-or-later); floating adapts FloatingItems by Ab5oluteZer0 (MIT).

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted copyrights and permission notices remain in source headers and `THIRD_PARTY_NOTICES`.

Author: expDARE.
