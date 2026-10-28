## Changes in 0.1.17

- Ship speed multipliers now reflect Forward Drag changes.

# Keel

Version 0.1.17. Requires Crucible 0.3.32 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

Tune every ship, level a Sailing skill, and build eight new ships from the vanilla hulls.

## Features

- Ship tuning, per ship and for all ships: health, sail and paddle force, turning, drag, damage taken, weather wear and build cost.
- Sailing skill: steering a ship earns experience (crew aboard get a share). Higher levels make the ships you build sturdier, the ships you steer faster, and widen what you explore at sea.
- Wind Call (Sailing 25): turns the wind to blow the way you look while you steer. Use your own key or gamepad chord.
- Lookout (Sailing 50): a pulse from your ship that shows the name tags of nearby creatures for a short time. Use your own key or gamepad chord.
- Eight new ships built from vanilla hulls: Fast Karve, Fast Longship, Knarr, Busse, Big Busse and three Byrdings with livestock pens.
- Each new ship has its own recipe, size, hold size, sail color, hull color and stripes, and can be hidden from the hammer.
- Byrding deck pens with rails and side gate ramps that keep animals aboard while the ship moves.
- Ship panel with your speed, a farther boat camera for big ships, see-through sails for the ship you steer, and names for vanilla ships.
- While you are aboard, a wind square in the column right of the minimap (with Mantle): an arrow showing where the wind blows relative to where you face, and its strength.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.32 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Keel with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Keel` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Keel on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.keel.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings, admins can lock settings (Server / Locked Player Settings), and locked settings show as read-only.
- Cheats: Ashlands Immune Ships, Invulnerable Ships, Tailwind (the wind always blows from behind the ship you steer, for you alone) and Fast Sailing (the ship you steer sails at up to twice its top speed) are off by default. In multiplayer they stay locked unless the server allows them.
- Converter: imports your ShipConfig, GrindstoneSkills (Sailing) and TheGreatestShips settings after a preview. The source files are not changed.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version. Ship prefabs and tuning must match on every machine.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Keel turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Keel. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Each module turns itself off when the mod it overlaps is installed and active: ship tuning for ShipConfig, the Sailing skill for GrindstoneSkills (when its Sailing part is on), and the new ships for TheGreatestShips. Keel says so in game.
- Optional Meadworks integration: Shepherd sees the Byrding pens and uses each pen's capacity as its herd limit.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): TheGreatestShips (DeathMonger).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Keel

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Ship tuning and Sailing logic are adapted from ShipConfig and GrindstoneSkills by Gerald Glasgow (GPL-3.0-or-later). The Sailing skill icon is GrindstoneSkills' AI-generated `skill_sailing.png` by Gerald Glasgow, used unchanged under GPL-3.0-or-later (not CC BY-NC-SA). The new ship types follow the idea, numbers and pen behavior of TheGreatestShips by DeathMonger, credited as a behavior reference (no code used).

Author: expDARE.
