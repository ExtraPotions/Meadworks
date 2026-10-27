## Changes in 0.2.3

- Circlet light search runs twice a second instead of every frame when no lit circlet is worn.
- Removing a floor or roof piece skips the collider walk when no tag changed.

# Timber

Version 0.2.3. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

Building tools: a build camera, flooring options, area repair and server-controlled building cheats.

## Features

- Build camera: press Shift + B (or the gamepad left trigger + right stick chord) to fly a camera around your build area and place, remove and repair pieces with the normal hammer rules. It works within your crafting station's area and does not pass through terrain.
- Camera options: world-relative movement, separate mouse and gamepad axis inversion, Extra Reach (95 meters more build distance from the camera by default, 100 m in all), picking up nearby items within 10 m of the camera by default, a following Wisplight, circlet light color and shadows, and a first-person zoom hold.
- Opening your inventory returns control to your character; other windows pause the camera.
- Flooring (all on by default): Fires On Floors (fireplaces and fire comfort pieces on wood floors), Smelters On Floors, Floors Are Roofs (floors shelter the space below) and Floors Dont Decay (floors do not take rain damage).
- Console: `timber bindings` reports possible key binding conflicts with other mods; `timber import` is described under Configuration.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Timber with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Timber` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Timber on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.timber.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings (such as flooring and camera reach), and admins can lock settings; locked settings show as read-only.
- Admins can also lock chosen personal settings for everyone with `Server / Locked Player Settings`. Your own values come back when you leave that server.
- Cheats: `Cheats / Ignore Support` (pieces you place while it is on stay up without support), `Cheats / Free Build` (hammer pieces cost no materials) and `Cheats / Build Everywhere` (place and remove pieces in no-build zones such as dungeons and villages; wards still apply) are off by default. In multiplayer they stay locked unless the server allows them.
- Import settings from other mods with `timber import cartur|azumatt|flooring|openkeep`, which shows a preview first; add `apply` to copy them. Your old config files are not changed and a backup of Timber's settings is required first.
- Keys: Shift + B toggles the build camera. It and the gamepad chord can be changed in the settings.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Timber turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Timber. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: if Cartur's Build Camera or Build Camera Custom Hammers Edition (Azumatt) is loaded, or OpenKeep is loaded with its Build Camera enabled, Timber turns off only its camera.
- If Cartur's Flooring is loaded, Timber turns off its flooring options. If OpenKeep's Build On Wood list is not empty, Timber turns off only Fires On Floors.
- Converter: imports settings from Cartur's Build Camera, Build Camera Custom Hammers Edition, Cartur's Flooring and OpenKeep. OpenKeep's blueprint settings are skipped.
- Meadworks: with Magpie, the camera's item pickup uses Magpie's pickup rules. Each mod also works alone.

## Notes

- Blueprints, construction sites, copy building, fix ground and the site planner were removed and will not return. Worlds that still hold Timber construction posts load normally; the posts no longer appear.
- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): Carturs_Flooring (Cartur).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Timber

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

The build camera adapts OpenKeep's BuildCamera by Gerald Glasgow (GPL-3.0-or-later) and Cartur's Build Camera (GPL-3.0-only); the support-wear switch adapts BuildPieceTweaks by aedenthorn (public domain). Exact sources: SOURCE_NOTICE.md. Carturs_Flooring is a behavior reference only; no code used.

Code: Timber's own code and the OpenKeep portions are GPL-3.0-or-later; the Cartur portions are GPL-3.0-only, so the combined distribution is GPL version 3 (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`).

Author: expDARE.


Optional `Repair / Area Repair` is off by default. After one successful hammer repair, it repairs up to 64 damaged pieces touching that center, using each neighbor's native station and ward checks. It does not spread through a building or add swing costs. OpenKeep Area Repair takes precedence while active.
