# Beacon

Version 0.1.14. Requires Crucible 0.3.26 / Crucible.UI 0.3.19. Changes are in [CHANGELOG.md](CHANGELOG.md).

Saved servers that join without the password prompt, with passwords kept in your system's protected storage.

## Features

- A Beacon panel beside the Join Game server controls: add, edit, rename (aliases), reorder and remove saved servers, and choose a default server.
- Saved servers are added to the Join Game Favorites tab without renaming or reordering your existing favorites.
- Joins a saved server without the password prompt. After a server accepts a password you typed, Beacon offers once to save it.
- Passwords are stored with Windows Data Protection or the Linux Secret Service keyring, never in Beacon's server list. Without a usable Linux keyring, passwords are kept for this session only, or not at all (your choice).
- Masked password entry. Leaving the password blank keeps the stored one; Forget password removes it.
- Removing a favorite in game also removes the Beacon entry, with a 15-second Undo.
- Optional auto-join after start: picks the default (or a named) server and character after a short countdown you can cancel.
- Optional reconnect after an unexpected disconnect (off by default). Never after a kick, a ban or logging out.
- Import from AutoConnector in Almanac's Convert tools or with console commands: a password-free preview first, then Apply. After a successful import Beacon can blank the plain-text passwords in AutoConnector's file, after a confirmation.
- Console commands: `beacon status`, `beacon join <name>`, `beacon cancel`, `beacon import preview`, `beacon import apply`, `beacon import blank-source confirm`.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.26 or a later 0.3.x, which includes Crucible.UI 0.3.19 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Beacon with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Beacon` folder from this package into `BepInEx/plugins/`.
- Install it on your own game; the server and other players do not need it (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.beacon.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- Panel Key (empty by default) adds a keyboard shortcut for the Beacon panel; otherwise use the button on the Join Game screen.
- Server admins: on a server running Beacon, set Server / Allow Automatic Reconnect to false to stop Beacon clients from reconnecting automatically after an unexpected disconnect. A dedicated server running Beacon only applies this setting; it starts no menus or password storage.
- Beacon has no cheat settings.

## Multiplayer

- Client-optional: install it on your own game; the server and other players do not need it, and versions are not checked. It works on any server, modded or vanilla. If the server also runs Beacon, it can turn off automatic reconnect for its players.
- Beacon does not stop players without Meadworks from joining: an admin can set Crucible's `Network.AdmissionMode` to `AllowVanilla`, and it works when every other installed mod allows it.

## Compatibility

- Steps aside: when AutoConnector is installed, Beacon leaves joining to it and says so; the rest of Beacon keeps working.
- Converter: imports AutoConnector's server list and moves its passwords into protected storage (see Features).

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): AutoConnector (MidnightMods).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Beacon

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Favorites, password join and the converter follow the behavior of AutoConnector by MidnightMods, credited as a behavior reference (no code used).

Author: expDARE.
