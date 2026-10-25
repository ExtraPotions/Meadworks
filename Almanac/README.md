## Changes in 0.1.23

- Guard pause handling when the settings window or game instance is unavailable.

# Almanac

One in-game settings book for every installed mod's configuration, aware of server locks.

Version 0.1.23. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- One window lists every installed BepInEx plugin that has settings, Meadworks or not.
- Each setting gets a typed editor (toggles, numbers, choices, text and key bindings) and always says whether you can change it here, and if not, why.
- Hover a setting or its control for the mod's explanation, current/default values, allowed range and permission details. Toolbar and utility actions explain their purpose too.
- Search, pinned plugins, per-section and per-mod collapse, and highlighting of settings changed from their default or during this session.
- Key binding capture with conflict warnings.
- Session change list with undo, reset-to-default previews, saved profiles and automatic backups.
- Edit as text: change a mod's config file as text; each changed line still goes through that setting's own checks, so locks and validation apply. Files no plugin owns stay read-only.
- World group: products that support world settings show a World group with the world value, its source (World or Config) and a Use Config button that removes only the world override.
- Logging controls and data-file pages registered by other Meadworks products, plus their settings converters.
- Opens from the main and pause menus. Toggle Key is optional and unbound by default; coexistence controls preserve another manager's shortcut.
- Window opacity and an optional Pause Game (single player only).

The Advanced switch in the header controls specialist settings. Pin/Unpin manages the selected mod's place in the list; raw pinned identifiers remain available under Advanced. Internal remembered selection is hidden. Product list width and Setting descriptions clarify the existing preferences without changing their saved keys or values. Imported dock preference is retained under Advanced for compatibility; Almanac currently uses a floating window.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2 (installed with Crucible)
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Almanac with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Almanac` folder from this package into `BepInEx/plugins/`.
- Install it on your own game; the server and other players do not need it (see Multiplayer).

## Configuration

- Accepted own-value edits save by default, including local preferences in files that also contain server settings. Almanac preserves unrelated saved keys and never copies live server overlays into your local preferences. A failed save is reported as a failed edit; remote administrator requests remain pending until the server confirms them.
- Settings file: `BepInEx/config/com.meadworks.almanac.cfg`. Edit it in game with Almanac itself or any ConfigurationManager.
- Settings that a server synchronizes are shown read-only to non-admin players, with the reason. Hidden server settings stay hidden.
- On a Jotunn-synced server, an admin's edit shows as pending until the server confirms it.
- Almanac has no cheat settings.
- Mods that ship their own ConfigDrawers drawer show it in their row, built with Almanac's controls; locked settings stay read-only, and a drawer that fails twice falls back to the normal editor.

## Multiplayer

- Client-optional: install it on your own game; the server and other players do not need it. Almanac's own settings are personal to each player and the server cannot change or lock them; Almanac does show the server's locks on other mods' settings.
- Almanac does not stop players without Meadworks from joining: an admin can set Crucible's `Network.AdmissionMode` to `AllowVanilla`, and it works when every other installed mod allows it.

## Compatibility

- Almanac never patches, disables or edits another config manager (BepInEx ConfigurationManager, shudnal's ConfigurationManager, Vapok's ConfigDrawers). If another manager owns F1, Almanac uses Left Alt + F1 and says so once.
- Converter: imports your ConfigDrawers window preferences (dock side, width, opacity, advanced view, file watching) after a preview, and asks for a backup first. It copies preferences only.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Almanac

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). No third-party source is adopted; conventions read by name are listed in `LICENSE_SCOPE.md`.

Author: expDARE.
