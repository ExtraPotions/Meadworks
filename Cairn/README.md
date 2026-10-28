# Cairn

See more of the world: an exploration radius that grows on high ground and at sea and shrinks in forest, at night and in fog; an Exploration skill; per-effect fog controls; a time, weather and compass HUD; and tidy map pins.

Version 0.1.21. Requires Crucible 0.3.36 / Crucible.UI 0.3.25. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- Exploration radius: the map uncovers further on high ground and at sea, and less in forest, at night and in fog. Keel's Sailing skill and Cairn's Exploration skill add to it.
- Exploration skill: grows as you uncover new map area, find new pins from Vegvisirs and runestones, and reach new biomes. As it grows your land radius gets larger and night and weather losses get smaller. Map reveals and pins from cheats, the console or other mods earn nothing.
- Vantage: from Exploration level 50, Vantage uncovers twice the radius after you stand still on high ground (off while Pathfinder runs the radius).
- Fog, mist and smoke removal: one toggle per effect (distance fog, ground mist, fog clouds, Mistlands mist, cave mist, smoke and more). All effects stay on by default, so installing Cairn changes nothing until you switch one off.
- Time, weather and compass HUD with the map markers you choose, live radius rows and Tempest warnings. It uses a fixed default position; show or hide it from the Meadworks menu (F8 or Esc > Meadworks), with a gamepad chord, or with a key you bind. Marker choices are saved per character and world. `4 - HUD / Compass Strip Width` widens the compass (240 to 640).
- Weather forecast on the clock line: the next weather in your biome, its name, the minutes until the shared world clock changes it, and the wind direction and strength at that change. `4 - HUD / Show Forecast` is your own setting and is on by default. The line stays hidden during a raid, a forced environment or a Tempest event, and it leaves out environments Tempest has switched off. A seasonal name from Solstice, such as Clear Winter, uses the name and icon of the weather it copies. The forecast reads the current season's list, so a change after the season turns can differ. Without Solstice, or with its seasons off, the line uses the game's own weather. The current weather stays beside the forecast when both fit.
- Sleep row under the minimap, shown while Ember reports sleep and no window or the large map is open.
- Sleep row icons: the moon means it is night and beds can be used; the bed and count (for example 1/3) are players in bed out of players online; +N% is how much faster the night passes while some players sleep; N% without a plus is the skip to morning once enough players sleep.
- A Comfort square in the column right of the minimap (with Mantle): your comfort level over the Rested icon, with the Rested time left while you are rested.
- Tidy map pins (optional, off by default): smaller icons (player, ping and shout pins keep their size), faded crossed-out pins, a larger pin under the cursor, and names only under your mouse or the gamepad map cursor. Hold Left Alt on the large map to see every name (`7 - Map / Show All Names Key`). Turn it on with `7 - Map / Tidy Pins`; turning it off restores the previous look.
- Settings and markers in a five-tab window, opened with **HUD settings** in the Meadworks menu (F8 or Esc > Meadworks); Toggle Key and Settings Key are unbound by default.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.36 or a later 0.3.x, which includes Crucible.UI 0.3.25 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Cairn with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Cairn` folder from this package into `BepInEx/plugins/`.
- Install it on your own game; the server and other players do not need it (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.cairn.cfg`. Edit it in Cairn's own window (Meadworks menu), in game with Almanac (the Meadworks settings book) or with any ConfigurationManager.
- In multiplayer the server's values win for server-synced settings and admins can lock settings; locked settings show as read-only. A server running Cairn can keep any fog effect on for everyone.
- Cairn has no cheat settings; map reveal is in Hex.
- Converters: preview Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud settings, then apply with a required backup. NoFogBruh and BetterFog settings are merged when both are present. TimeWeatherHud marker choices are applied to their matching character and world, with your previous choices backed up. The other mods' files are not changed. Import while disconnected or hosting; import is refused while connected to someone else's server.

## Multiplayer

- Client-optional: install it on your own game; the server and other players do not need it. If the server also runs it, versions are checked and the server can lock individual settings.
- On a server without Cairn, Mistlands mist, particle mist and cave mist stay on and the exploration radius is capped at Cairn's default values (land 200 m, sea 300 m, maximum 2000 m).
- Cairn does not stop players without Meadworks from joining: an admin can set Crucible's `Network.AdmissionMode` to `AllowVanilla`, and it works when every other installed mod allows it.

## Compatibility

- Steps aside, module by module, and says so once: Pathfinder turns off Cairn's exploration radius; NoFogBruh or BetterFog turns off Cairn's fog controls; TimeWeatherHud (while its HUD is enabled) turns off Cairn's HUD. Everything else keeps running.
- Converters: Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud settings and markers (see Configuration).
- Optional Meadworks integrations: Keel's Sailing skill adds to the radius; the HUD shows Tempest weather warnings and Ember's sleep status when those products run.

## Notes

- Gamepad: supported through the shared Crucible.UI controller navigation and configurable pad chords.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired by / replaces (behavior reimplemented, no code reused): TimeWeatherHud (Lazly), TidyPins (SirJose).
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: Corresponding Source for this exact version, free of charge: https://github.com/ExtraPotions/Meadworks/tree/main/Cairn

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

The exploration radius adapts Pathfinder by Crystal Ferrai (Apache-2.0); fog controls adapt NoFogBruh by Pete Navarra (aka Vapok) (MIT) and BetterFog by JevMods (MIT); the sleep row's layout adapts BetterSleepBruh by Pete Navarra (aka Vapok) (MIT).

Code: GPL-3.0-or-later (`LICENSE-CODE.md`, `licenses/GPL-3.0.txt`). Original documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). Adopted sources and notices: `LICENSE_SCOPE.md`, `SOURCE_NOTICE.md`, `THIRD_PARTY_NOTICES/`.

Author: expDARE.
