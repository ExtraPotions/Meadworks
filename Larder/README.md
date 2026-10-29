## Changes in 0.2.20

- Requires Crucible 0.3.36 (dependency pin updated so mod managers install the current Crucible).

# Larder

Version 0.2.20. Requires Crucible 0.3.36 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

Food and mead values, eating, regeneration, stamina costs, kitchen stations and Cooking skill perks.

## Features

- Food and mead: change food values and duration, how food fades, eating again before a food runs out, the number of food slots, and auto eat.
- Recovery: health, stamina and eitr regeneration rates and delays, rested bonuses, and regeneration while blocking, encumbered or swimming. Rested Delay changes the wait while naturally resting (0-60 seconds); its default comes from the game and leaves the native hook untouched.
- Exertion: stamina costs of running, jumping, dodging, sneaking, swimming, blocking, attacks, tools and fishing, with separate out-of-combat costs, plus base health and stamina.
- Safe Stamina (off by default): chosen actions (sprint, jump, build, swim, attack, sneak) cost no stamina while no enemy is within 25 meters. Safe Area Ignored Creatures accepts a comma-separated list of prefab or displayed names; empty ignores nothing. Safe Area Encumbered is a separate opt-in for free encumbered walking and regeneration while safe; it is off by default.
- Kitchen stations: cook times, burning, fermenting and feasts.
- Cooking skill perks, with the Cooking skill icon on the skill page.
- `6 - Kitchen / Fermenter Timer` (off by default, your own preference): hovering an accessible, sheltered fermenter that is still fermenting shows the time left until it is ready, while Cooking is on. Empty and finished fermenters do not show it.
- Every setting at its default leaves the game as it is.
- No dish or item stars. An egg keeps the hen's stars as its item quality. A recipe that needs every ingredient counts that quality. A recipe that accepts only one of its ingredients does not.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.36 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Larder with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Larder` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Larder on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.larder.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for gameplay settings and admins can lock settings; locked settings show as read-only. Fermenter Timer is your own preference.
- Cheats in `9 - Cheats`: Instant Kitchen, Endless Food and Eat Anything, all off by default. In multiplayer they stay locked unless the server allows them; a blocked cheat reads off.
- Converter: preview and apply your FeastMaster and GrindstoneSkills (Cooking) settings from the `Import` section or Almanac, with one backup first. The source files are left unchanged; GrindstoneSkills star settings are reported, not imported.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Larder turns off for that session and cheats stay locked.
- Players without Meadworks (vanilla clients) cannot join a server running Larder. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside: with FeastMaster installed, Larder's food and kitchen rules turn off; with GrindstoneSkills installed, Larder's Cooking rules turn off.
- Converter imports: FeastMaster and GrindstoneSkills settings (see Configuration).
- Optional Meadworks integrations: with Mantle, auto eat prefers food in Mantle's Food slots.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): Cartur's Safe Stamina (Cartur), FermenterTimer (Tie).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Larder

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Food, kitchen and Cooking rules adapt FeastMaster and GrindstoneSkills by Gerald Glasgow (GPL-3.0-or-later); item copies use ValheimModLibs by Gerald Glasgow (GPL-3.0-or-later). The Cooking skill icon is GrindstoneSkills' icon by Gerald Glasgow (GPL-3.0-or-later).

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources, art and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`, `THIRD_PARTY_NOTICES/`.

Author: expDARE.
