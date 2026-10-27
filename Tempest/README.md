# Tempest

Version 0.1.16. Requires Crucible 0.3.30 / Crucible.UI 0.3.21. Changes are in [CHANGELOG.md](CHANGELOG.md).

Rare dangerous weather: blizzards in the lowlands and the Deep North and severe thunderstorms, announced five minutes ahead and the same for every player.

## Features

- Rare blizzards in the Meadows and Black Forest that freeze the unprepared as the Mountains do. In the Deep North a blizzard is a whiteout: the twilight snowstorm with thicker fog and full wind, under the same warning, notice and Cairn badge.
- Severe thunderstorms that drive ground lightning across the lowlands and the sea. Player bases are spared from lightning.
- Every event is announced five minutes ahead. With Cairn installed, the warning also shows on Cairn's weather HUD.
- The server decides the weather, so every player sees the same event at the same time, and an event survives a restart.
- Weather filter: the server can switch off individual game weathers.
- Tempest events never occur in the Mountain or Mistlands, and only a blizzard reaches the Deep North. Biome settings drop names a kind does not cover, and the server log says so once. An existing host config keeps its saved Blizzard Biomes; add DeepNorth there to get the whiteout.
- Tempest yields to the game's own raids and random events: no new event starts over a player inside one, and players inside one get no Tempest strikes, hazards or messages while the raid's sky shows.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.30 or a later 0.3.x, which includes Crucible.UI 0.3.21 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Tempest with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Tempest` folder from this package into `BepInEx/plugins/`.
- In multiplayer, install Tempest on the server and on every client (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.tempest.cfg`. Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for weather settings and admins can lock settings; locked settings show as read-only.
- Cheats: Weather Commands (summon or stop an event) and Personal Weather Override, both off by default. In multiplayer they stay locked unless the server allows them.
- Converter: imports NoFogBruh's weather switches into the weather filter after a preview, from Almanac's Convert tools or the console (`tempest import` previews, `tempest import apply` imports). Host or single player; Tempest's settings file is backed up first.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version, so the weather matches on every machine.
- Joining a server without Meadworks: Crucible waits for the server's reply (10 seconds by default), then treats it as a vanilla server; Tempest turns off for that session and cheats stay locked. Tempest also turns off on a Meadworks server that does not run Tempest.
- Players without Meadworks (vanilla clients) cannot join a server running Tempest. With only server-only or client-optional Meadworks products installed, an admin can let them join with Crucible's `Network.AdmissionMode` set to `AllowVanilla`.

## Compatibility

- Steps aside, module by module, and notes it in the log: WildWeather turns off Tempest's weather events; NoFogBruh turns off the weather filter; Valheim_Admin_Tool turns off the personal weather override.
- Converter imports: NoFogBruh's weather switches.
- Optional Meadworks integration: Cairn shows Tempest warnings on its HUD.
- Solstice is optional: seasonal weather names use their base filter rules, and host-owned seasonal weights can adjust ordinary weather. Empty defaults keep existing weights. Tempest owns weather selection and events; Solstice owns season facts. See [the season contract](docs/SEASONS.md), including Cairn forecast limits.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): WildWeather (SirJose).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Tempest

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). The weather filter is adapted from NoFogBruh by Pete Navarra (aka Vapok) (MIT). The weather events follow the idea and behavior of WildWeather by SirJose, credited as a behavior reference (no code used).

Author: expDARE.
