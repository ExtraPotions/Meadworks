# Thing

Version 0.1.12. Requires Crucible 0.3.28 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

Server administration and moderation from inside the game, checked against the server's own admin list.

## Features

- Administration window for admins (the Meadworks menu: F8 or Esc > Meadworks; Admin Window Key, unbound by default), with gamepad support. Players are identified by platform ID even when names match.
- People: see who is online, kick, ban permanently or for a limited time, unban, warn and mute with reasons; edit the permitted list, and the admin list when the server allows it.
- Freeze: an admin can freeze or unfreeze a connected player for this connection only. The frozen player cannot move or use actions. Menus and chat still work. The flag clears on unfreeze and on disconnect, and it is not saved.
- Permissions: the host or a listed admin can grant World tools, Spawn, Teleport, Freeze, Inventory and Ward for one platform id, including an id that is offline. Grants are stored on the server for that world. Listed admins keep every right. Everyone else starts with none. The People page edits the grants. A player who holds one of these grants can open the People page. The other pages stay with a listed admin. A grant does not turn on a server switch that is off.
- Inventory: an admin with the Inventory right can view a connected player's inventory (up to 48 stacks) and Take or Delete one stack. Take removes that stack from the player and puts it in an empty slot in your inventory as its own stack, or leaves it at your feet when no empty slot is free. It does not merge into a stack you already have. Delete asks a second time and is written to the server log. Custom data, quality, durability, crafter and variant are kept. Closing the window drops a delete that is still waiting for the second confirm.
- Moderation log and the most recent audit records.
- Messages: broadcast to everyone (center or top-left), schedule or cancel a shutdown, save the world, and edit the message of the day and recurring announcements.
- World tools: show the world seed, set or freeze the global time, a session-only ward bypass, terrain reset in a previewed circle, and removal of loose dropped items, each with a confirmation step.
- Player teleport: `thing goto <player>` and `thing bring <player>`, optionally asking the player first.
- Server configuration: browse the server's config files and edit settings from in game, with a backup, and a check that the new value was saved. Files no plugin owns stay read-only.
- With Delve installed: a Regeneration page with Delve's status, recent actions, known dungeon and resource clocks and reset requests; Regenerate nature and Reset resources on the World page (`thing regenerate nature`, `thing resetresources`) bring back felled trees, mined rocks and picked resources in generated zones around the admin, never near player buildings.
- Admin pickup radius: `thing pickup <1-10>` or the World page sets your own item pickup radius in meters; `thing pickup reset` (or leaving the world, or losing admin) puts back the game's 2 m.
- Console commands for the same actions, for example `thing say`, `thing terrain`, `thing drops`, `thing ward` and `thing seed`.
- Every action is checked on the server against its own `adminlist.txt` by platform ID, never by display name, and refusal reasons reach the player in their language.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.28 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Thing with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Thing` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Thing on the server and on every client (see Multiplayer). Delve is optional and installed separately.

## Configuration

- Settings file: `BepInEx/config/com.meadworks.thing.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- The server's values win for server settings (for example whether non-admins may reset terrain around their own pieces or remove drops) and admins can lock settings; locked settings show as read-only.
- Thing has no cheat settings.
- Optional Ward Override and Remove Drops keys are empty by default.
- Converter: imports Valheim_Admin_Tool's Ward override and Remove drops hotkeys.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version. The server delivers refusal reasons, warnings and localized messages through each player's Thing.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Thing turns off for that session. Thing also turns off on a Meadworks server that does not run Thing.
- Players without Meadworks (vanilla clients) cannot join a server running Thing. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: if Valheim_Admin_Tool is installed, Thing hides only the world tools both offer and says so.
- Converter imports: Valheim_Admin_Tool's Ward override and Remove drops hotkeys.
- Optional Meadworks integrations: Delve (Regeneration page), Hex (boss and trader location lookups and spawn audit).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): Valheim_Admin_Tool (QoLTeam), administration part.
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Thing

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). The terrain reset adapts TerrainReset by aedenthorn (public domain); details in `SOURCE_NOTICE.md`. Other world administration rules follow the documented behavior of Valheim_Admin_Tool by QoLTeam (behavior reference only; no code used).

Author: expDARE.
