# Solstice

Four seasons for Valheim: spring, summer, fall and winter follow the world calendar and change how the world works.

Version 0.1.5. Requires Crucible 0.3.28 / Crucible.UI 0.3.20. Changes are in [CHANGELOG.md](CHANGELOG.md).

## Features

- A season calendar from the in-game day: four seasons of ten days by default, each length yours to set. The server decides, and the season is saved with the world, so every player is in the same season after they receive it. The season already underway keeps its start day when a length changes.
- Seasonal effects: plants grow fast in spring and not at all in winter, bees work hard in summer and rest in winter, food and stamina last longer in summer and run out sooner in winter, fires burn through fuel faster in winter, sap flows best in spring, the Rested effect lasts longer in summer, the wind blows harder in fall, and winter rain no longer wears down buildings. Every number is a setting per season. Places the seasons do not reach: indoors (a greenhouse under a roof), the Ashlands, the Deep North, high mountains, and inside a shield generator dome (in winter, by default). In a winter that stops growth, plants that are not hardy die of cold unless a fire is near; carrots, barley, flax, thistle and magecap are hardy (a server setting).
- Seasonal weather: each season adds and swaps weather in the biomes (more thunder in fall, snow in the Meadows, the Black Forest and the Plains in winter, clear heath skies in the Meadows in summer). Names such as Clear Winter are copies of the game's own weather with a night tint.
- A season status outdoors (not in the Ashlands or the Deep North), and overheating from too many warm clothes in summer unless you eat Eyescream, the weather is cold, or you are sheltered or indoors. Ripe crops on a server list can freeze and perish in a winter that stops growth. A crop that fails its winter survival roll drops its normal harvest once after the full freeze grace. Sleep and time skips do not shorten the grace.
- Works with Tempest: while a Tempest blizzard or thunderstorm reaches you, or a weather is forced, Solstice never resets the sky under it. Cairn, Tempest and other mods can read the current season from Solstice.
- On your own screen: grass, leaves and the ground take the season's color, and both maps tint with them. In winter, Meadows, Black Forest, Swamp and Plains ground takes the game's mountain snow, a little under full white, grass thins and frosts, leaves and small ground plants take a light snow and trunks stay brown. Materials that already know snow can still raise it. On the frozen days, shallow water tints toward icy blue and stays partly transparent. A small plate shows the season and day, with a season mark. Winter keeps that mark on the status when season icons are off. A heat meter shows warm-clothes heat in a hot season, and one of the game's own tracks can follow the season. These are yours to switch off, and they do not change the world for anyone else. There is no snow mesh and no ice you can walk on.
- A short message when a new season starts, and a console command: `solstice` shows the season and day.
- A cheat, off by default: Season Override holds the world in one season.

## Requirements

- BepInExPack Valheim 5.4.2351
- Jotunn 2.30.2
- Crucible 0.3.28 or a later 0.3.x, which includes Crucible.UI 0.3.20 (the shared Meadworks interface kit)

## Installation

- **Mod manager (recommended):** install Solstice with r2modman, Gale or the Thunderstore Mod Manager; the dependencies above are installed for you.
- **Manual:** install BepInExPack Valheim, then Jotunn and Crucible, then copy the `plugins/Solstice` folder from this package into `BepInEx/plugins/`.
- Solstice must also be installed on the server (see Multiplayer).

## Configuration

- Settings file: `BepInEx/config/com.meadworks.solstice.cfg` (Solstice 0.1.2 and earlier used `com.extrapotions.solstice.cfg`; its values are carried over on the first start and the old file is kept as `com.extrapotions.solstice.cfg.bak`). Edit it in game with Almanac (the Meadworks settings book) or any ConfigurationManager.
- In multiplayer the server's values win for the calendar and the effects, and admins can lock settings; locked settings show as read-only.
- General: Enabled turns the effects and the seasonal weather off for everyone while the season stays readable by other mods.
- Calendar: Days In Spring, Summer, Fall and Winter (1 to 100 each).
- One section per season (Spring, Summer, Fall, Winter): Plants Growth, Beehive Production, Food Drain, Stamina Drain, Fireplace Drain, Sap Collecting Speed, Rested Buff Duration and Wind Intensity multipliers, and Rain Protection.
- Protection: Heat Protects Crops, Shield Generator Protects, Shield Generator Protects In Winter Only, Winter Hardy Crops (the original mod's defaults). Crops Perish, Perish From Day, Freeze Seconds, Winter Harvest Chance and Crops To Control.
- Climate: Season Status, Warm Clothes Overheat and Cooling Foods.
- Notices: the season message is yours to switch off.
- Visuals: World Tint, Snow Cover, Frozen Water, Minimap Tint, Season Indicator, Season Icons and Heat Meter. Music: Seasonal Music. Each one is local to this player, takes effect at once, and the server does not override it. A dedicated server does not play music or draw the marks.
- Cheat: Season Override (off by default). In multiplayer it stays locked unless the server allows it.

## Multiplayer

- Required on all peers: install it on the server and on every client, same major.minor version. The server saves the season with the world and clients read it. Until that arrives, a client does not apply a season of its own.
- Joining a server without Meadworks: Crucible waits for its server Hello, then treats a server that does not reply as vanilla; Solstice turns off for that session. The wait is Crucible's local `Authority / VanillaServerTimeoutSeconds` setting, 10 seconds by default. It also turns off on a Meadworks server that does not run Solstice. The server's separate 60-second peer-answer window is for clients joining a Meadworks host.
- Players without Meadworks (vanilla clients) cannot join a server that runs Meadworks: Crucible is required on every peer.

## Upgrading from 0.1.2 or earlier

Solstice's config owner changed from `com.extrapotions.solstice` to `com.meadworks.solstice` in 0.1.3. The first start carries over the settings file and seasons-import marker when their current files do not already exist. Existing current files take priority, and old files remain recoverable.

Per-cheat player permissions are stored separately by Crucible under `Mischief / Players May Use`. Each entry includes the config owner, section and key. The old Season Override grant is `com.extrapotions.solstice|Cheats.Season Override`; the current entry is `com.meadworks.solstice|Cheats.Season Override`. Settings-file migration does not migrate this permission list, and an old-owner entry does not grant the new-owner cheat.

After backing up the host/server settings, an administrator can review Season Override's player permission in the MeadWorks menus and explicitly allow the current entry if intended. Preserve the old records and unrelated grants while reviewing them. Category permissions and other cheats retain their existing policy; the rename does not automatically broaden access.

## Replaces / compatibility

- Steps aside and says so when Seasons (shudnal) or Seasonality (RustyMods) is installed.
- Tempest: Solstice changes only the biome weather lists and never forces weather, so a Tempest event, a forced environment or a personal weather override keeps the sky. At a season start Solstice also waits (at most an hour) before making the game choose weather again if one of those is active.
- Not in Solstice: copied season textures, snow meshes, ice you can walk on, Seasons' summer-heat meter, and Seasons' music files. Snow cover and frozen water are still numbers other mods can read. The client color does not write snow onto buildings. The heat meter only shows the warm-clothes overheat Solstice already applies. Seasonal music uses tracks the game already has. Ember (torches and night), Shepherd (breeding) and Wildblood (wood and meat) can read those numbers from Solstice. See docs/SEASON-CONTRACT.md.
- Not yet tested side by side with other weather or time mods.

## Compatibility & notes

- Cheats: anything this product's cheats spawn carries Valheim's native cheated flag (m_cheated); servers running CheatTagEnforcement remove those items. In multiplayer, cheats stay locked unless the server allows them.
- Gamepad: the season plate is display only. It has no control to focus.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (notices, season names, the console reply, the season plate, the heat meter, and the visual and music settings; the configuration file's own descriptions stay English). The translations await a native review.
- The package icon is a sun drawn for Solstice in the Meadworks icon style (see docs/art-provenance.md).
- Adapted from Seasons by shudnal (Unlicense, public domain): the calendar arithmetic, the default seasonal values and weather rules, and the effect patches. Seasons' textures and music are not used.
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: https://github.com/ExtraPotions/Meadworks/tree/main/Solstice

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Code: GPL-3.0-or-later (`LICENSE-CODE.md`). Original artwork and documentation: CC BY-NC-SA 4.0 (`LICENSE-ASSETS.md`). The season calendar, default effect values and weather rules are adapted from Seasons by shudnal, released into the public domain under the Unlicense (`THIRD_PARTY_NOTICES/Seasons-Unlicense.txt`). Product author: expDARE.
