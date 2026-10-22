# Bestiary

Version 0.1.18. Requires Crucible 0.3.28 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

New creatures with fights of their own, the bone arsenal you craft from them, and a creature workshop for server owners.

## Features

- New creatures: Crypt Mimic, Greydwarf Slinger, Rime Giant, Kraken, the arsenal skeletons, the Skeleton Crossbowman and the Crypt Executioner, each with its own attacks, spawns and loot.
- The bone arsenal made from them, including the Bone Crossbow with bolts and the Executioner's greataxe, with their recipes.
- Compendium pages: fourteen creature pages and four item-group pages in the game's existing list, covering habitats, attacks, loot and equipment use.
- Creature workshop: server owners can edit creatures, attacks, AI, factions and projectiles in YAML files listed in Almanac. The server checks each edit and sends a complete snapshot to connected players; an invalid edit keeps the last working state.
- Workshop console commands on the server (or single-player owner): `bestiary:reference creature|ai|attack|loadout|projectile|texture`, `bestiary:full creature` and `bestiary:reload`.
- Works on Windows and Linux.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.28 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)
- Wildblood 0.5.5 or later

## Installation

- **Mod manager (recommended):** install Bestiary with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn, Crucible and Wildblood, then copy the `plugins/Bestiary` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Bestiary on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.bestiary.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for every gameplay setting and admins can lock settings; locked settings show as read-only.
- Bestiary has no cheat settings.
- Workshop data files live below `BepInEx/config`. Turning the workshop off keeps the creatures and items it already added registered. Keep a custom definition while a world still contains its creatures or items.
- Converters (run from Almanac, with a preview and a backup first): imports Elite Creatures Pack settings once, and copies CreatureManager workshop files into Bestiary's workshop folder without changing the originals. Level and Karma files are left to Wildblood.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Bestiary turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Bestiary. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: when Elite Creatures Pack is installed, Bestiary turns itself off (both would add the same creatures). When CreatureManager is installed and has creature data, Bestiary's workshop turns off and the rest keeps running.
- Converters: Elite Creatures Pack settings and CreatureManager workshop files (see Configuration).
- Meadworks integrations: Wildblood supplies stars, mutations, loot rules and the Marrow Lantern's Reveal for Bestiary creatures, and mod managers install it with Bestiary. Inscribe supplies runes and item levels for the arsenal and is optional.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Bestiary

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Creatures and the arsenal adapt code from EliteCreaturesPack and ValheimModLibs by Gerald Glasgow (GPL-3.0-or-later); the creature workshop adapts CreatureManager 1.2.5 by sighsorry1029 (GPL-3.0-only).

Creature bodies, materials, shaders, sounds and effects come from the running game. Bestiary ships no Valheim meshes, textures or animation clips. Details are in SOURCE_NOTICE.md.

Code: original Bestiary code is GPL-3.0-or-later; the combined plugin is GPL-3.0-only because it includes CreatureManager code (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`, `THIRD_PARTY_NOTICES/`.

Author: expDARE.
