## Changes in 0.1.24

- Guard recipe, station and salvage paths when item or inventory data is unavailable.
- Leave Virtual Workbench Key unbound for new entries while preserving saved keys.
- Refresh tracker counts every 1.25 seconds while keeping explicit invalidation immediate.

# Kindle

Recipe search and sorting, favorites, batch crafting, a recipe tracker, salvage and an optional Cart Workbench.

Version 0.1.24. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Recipe search (accent-insensitive), category buttons (Ammo, Weapons, Bows, Armor, Shields, Tools, Food, Materials), sorting, per-character favorite recipes and a list or grid view, with keyboard and gamepad actions.
- Batch crafting: pick an amount; Kindle checks materials and inventory space, and keeps the amount while it crafts.
- Recipe tracker: track recipes per character and see material and station readiness and a craft countdown in a fixed HUD.
- Salvage tab: break items back into materials by the salvage rules (a share of the recipe), with inventory checks. With Inscribe installed, salvage shows Inscribe names, inscription details and rarity backgrounds, inscribed Magic and Rare items can be salvaged and may give a rune back (chance shown); Epic Loot rarity colors are shown when Epic Loot is installed. With Burrow installed, the tab also lists gear in nearby chests and marks each row "From a chest". It uses the chests Reach crafting may use: the same range, wards, privacy, allow and deny lists, and the per-player Reach toggle. Chests another player has open, and chests held by another transfer, are skipped. Favorites, equipped items and the other salvage rules still refuse a chest item. The hotbar rule applies only to your own inventory. Materials go into your inventory, and what does not fit drops at your feet. A chest item that moved is not salvaged. Without Burrow, the tab lists only your inventory.
- Repair On Station Open (off by default): opening a usable station runs the existing free repair-all action for its eligible carried gear.
- Cart Workbench (cheat, off by default): use a cart as a crafting station.
- Salvage rules are in `Kindle.Salvage*.yml` files that you can edit, or edit in Almanac's data pages.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Kindle with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Kindle` folder from this package into `BepInEx/plugins/`.
- Install it on your own game; the server and other players do not need it (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.kindle.cfg`, under Recipe List, Batch Crafting, Repair, Recipe Tracker, Salvage, Cart Workbench and Server. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- `Salvage / Nearby Chests` is on by default. On a server the host's value applies. In single player your own value applies. Turning it off lists only your inventory.
- Personal settings are your own. When the server runs Kindle, its values win for server-synced settings, and it can lock personal settings with `Server / Locked Player Settings`; locked settings show as read-only.
- Cheats: Craft Speed (1, normal speed, by default), Instant Crafting (off by default; every craft and batch finishes at once) and Cart Workbench (off by default). In multiplayer they stay locked unless the server allows them; on a server without Kindle they read 1 and off.
- Converter: imports your OpenKeep crafting settings after a preview, with a backup first.

## Multiplayer

- Client-optional: install it on your own game; the server and other players do not need it. If the server also runs it, versions are checked and the server can lock individual settings.
- Kindle does not stop players without Meadworks from joining: an admin can set Crucible's `Network.AdmissionMode` to `AllowVanilla`, and it works when every other installed mod allows it.

## Compatibility

- Steps aside: when OpenKeep (2.0 or newer) or Crafting Search is installed, Kindle turns itself off and says so.
- Converter imports: OpenKeep settings (see Configuration).
- Optional Meadworks integrations: Burrow supplies nearby chest counts, favorite protection and salvage from those chests (without it Kindle counts and salvages only your own inventory); Mantle's inventory layout is respected; Inscribe decorates salvage entries.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Kindle

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Recipe, batch, tracker, cart and salvage logic adapts OpenKeep 3.0.0 by Gerald Glasgow (GPL-3.0-or-later); YAML helpers adapt ValheimModLibs by Gerald Glasgow (GPL-3.0-or-later); recipe sorting and matching adapt CraftingSearch 1.0.2 by Ab5oluteZer0 (MIT).

Original code: GPL-3.0-or-later. Original artwork and documentation: CC BY-NC-SA 4.0. Retained source and runtime dependency notices are in `THIRD_PARTY_NOTICES/`; see [LICENSE_SCOPE.md](LICENSE_SCOPE.md).

Author: expDARE.
