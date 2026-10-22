# Meadworks

Dependency-only modpack for the Meadworks Valheim suite by expDARE. Installing it pulls in Crucible and every packaged Meadworks product; it contains no code of its own.

## Features

Included (27), by category:

**Library**

- **Crucible** (shared library): product registration, server authority, the shared interface kit, localization and logging. Adds no gameplay. Required on all peers.

**Hoard**

- **Burrow** (storage): craft and build from nearby chests, chest previews, bigger chests, sorting, quick stack, restocking, routing and multi-user chests. Required on all peers.
- **Mantle** (inventory and equipment): configurable grid, gear, ring and belt slots, backpacks, key ring, tacklebox and a Swap loadout button. Required on all peers (same version).
- **Magpie** (loot handling): magnet pickup, ground-stack merging, floating drops and a server-controlled loot multiplier cheat. Required on all peers.
- **Barter** (traders): per-trader buy and sell lists, custom currencies, a trader purse that moves prices, and a sell list beside the trader's window. Required on all peers.

**Hearth**

- **Wildroot** (gardening): grid planting, snapping, bulk harvest, replanting, a Farming skill with perks, seed crafting and more plantable flora. Required on all peers.
- **Larder** (food and kitchen): food and mead values, eating, regeneration, stamina costs, kitchen stations and Cooking perks. Required on all peers.
- **Shepherd** (animal husbandry): feed troughs, the Husbandry skill, breeding with sires, star inheritance, twins, herd limits and lineage. Required on all peers.
- **Ember** (home comforts): faster nights when some players sleep, torches that warm like a campfire, doors that open as you walk up. Required on all peers.

**Forge**

- **Kindle** (crafting): recipe search, sorting, favorites, list/grid views, batch crafting, recipe tracking, salvage and Cart Workbench. Client-optional.
- **Timber** (building): build camera, flooring options and area repair, with server-controlled cheats. Required on all peers.

**Fang**

- **Wildblood** (wild creatures): difficulty and stars that grow with the world, mutations and modifiers, boss aspects, loot rules and bad-luck protection. Required on all peers.
- **Bestiary** (creatures and bone arsenal): Crypt Mimic, Greydwarf Slinger, Rime Giant, Kraken, the arsenal skeletons, the Skeleton Crossbowman, the Crypt Executioner, and the Draugr Hook, Fuling Spearman, Slag Charred and Marrow Lantern. Required on all peers.
- **Inscribe** (gear inscriptions): item rarity, runes and enhancement tiers from +1 to +20 with scrolls; guaranteed upgrades as a server-controlled cheat. Required on all peers.
- **Edge** (combat): weapon-family secondary attacks, quickstep, slope-aware swings, swim equipment recovery and Dodge Cancel (off by default). Required on all peers.
- **Saga** (progression): boss kills turn the Forsaken powers into permanent passives, Defense, Sneak, Woodcutting, Mining, Foraging and Fishing gain perks, and one skill book shows every skill. Required on all peers.

**Waystone**

- **Cairn** (exploration): exploration radius, Exploration skill, per-effect fog controls, time/weather/compass HUD, map marker choices and tidy pins. Imports Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud settings. Client-optional.
- **Keel** (ships): tune every ship, grow a Sailing skill with Wind Call and Lookout, and build fast, cargo and livestock ships from vanilla hulls. Required on all peers.
- **Threshold** (portals): pick any portal from a list and the map, named hubs, access modes, favorites, optional fuel, quick jumps and sea gates for ships (off by default). Required on all peers.

**Runestone**

- **Almanac** (settings book): one in-game window for every installed mod's settings, showing which ones the server locks and why, with settings imports. Client-optional.
- **Tempest** (weather): rare blizzards and severe thunderstorms with a five-minute warning, the same for every player, plus a server weather filter. Required on all peers.
- **Thing** (server administration): moderation, messages, world tools and in-game server config editing, checked against the server's admin list. Required on all peers.
- **Delve** (world regeneration): dungeon sizes, chest refills, dungeon resources, the Queen and camp creatures come back on timers, never near players or their buildings. Server-only.
- **Beacon** (saved servers): join without the password prompt, passwords in protected storage, saved servers in Join Game favorites, optional auto-join and reconnect (a server can turn reconnect off), AutoConnector import. Client-optional.
- **Solstice** (seasons): spring, summer, fall and winter by the world calendar, with seasonal growth, bees, food, stamina, fire, sap, rest and wind, summer overheating, and local winter snow and season colors. Required on all peers.

**Mischief**

- **Hex** (cheat toolkit): personal powers, item and creature spawning, skills, map tools, ESP and presets, with server-controlled permissions. Client-optional.
- **Wayfarer** (map travel): Back to Bed for everyone, plus Stones, Anywhere, Boat, Grave and Player travel cheats the server switches on, usable by admins unless the server allows others. Required on all peers.

## Requirements

- BepInExPack Valheim 5.4.2351 and Jotunn 2.30.2 (installed with Crucible).
- Every product version is pinned in this pack's manifest.

## Installation

- **Mod manager (recommended):** install Meadworks with r2modman, Gale or the Thunderstore Mod Manager; every product and its dependencies are installed for you.
- **Manual:** not recommended for a whole pack; install each product's package as its own page describes.

## Configuration

Each product keeps its own settings file in `BepInEx/config/`. Edit them in game with Almanac (included) or any ConfigurationManager. In multiplayer the server's values win for server-synced settings.

## Multiplayer

- Most products are required on every peer, so the server and all clients should install the same pack version.
- Kindle, Almanac, Cairn, Beacon and Hex are client-optional. Hex is needed on the server and by every player who runs Meadworks; players without Meadworks can join alongside when the server allows them. Delve runs on the server and idles on clients.
- The pack includes products required on every client, so players without Meadworks cannot join a server running the full pack.

## Notes

- Gamepad: Meadworks windows can be used with a controller, and its button combinations can be changed in settings.
- Languages: English, Spanish, Simplified Chinese, Russian, French, German and Korean (non-English text is machine-assisted).
- Inspired-by credits for each product are listed in that product's README.
- Listed under Thunderstore's AI Generated category; tested in-game.
- Source: the pack contains no code; each product's page links its source.

## Changelog

See the Changelog tab (CHANGELOG.md).

## Credits and license

Contains no code or artwork of its own; each product's page lists its credits and licenses.

Author: expDARE.
