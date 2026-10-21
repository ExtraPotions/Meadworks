# Meadworks

Meadworks is a suite of Valheim mods by expDARE. Each folder holds the release source of one Thunderstore package
in the `expDARE` namespace; its README describes features, requirements and configuration.

Crucible is the shared library every product requires. The Meadworks modpack depends on all products.

| Mod | Version | Description |
| --- | --- | --- |
| [Crucible](meadworks-crucible/) | 0.3.26 | Meadworks shared library: product registration, server authority, the shared interface kit, localization and logging. Adds no gameplay. Required on all peers. |
| [Meadworks](MeadworksModpack/) | 0.1.8 | Meadworks suite: Crucible and all 24 Meadworks products in one install. Contains no code of its own. Required on all peers. |
| [Almanac](Almanac/) | 0.1.16 | Meadworks settings book: one in-game window for every installed mod's settings, showing which ones the server locks and why, with settings imports. Client-optional. |
| [Beacon](Beacon/) | 0.1.13 | Meadworks saved servers: join without the password prompt, passwords in protected storage, saved servers in Join Game favorites, optional auto-join and reconnect (a server can turn reconnect off), AutoConnector import. Client-optional. |
| [Bestiary](Bestiary/) | 0.1.15 | Meadworks creatures and bone arsenal: Crypt Mimic, Greydwarf Slinger, Rime Giant, Kraken, the arsenal skeletons, the Skeleton Crossbowman and the Crypt Executioner. Required on all peers. |
| [Burrow](Burrow/) | 0.3.24 | Meadworks storage: craft and build from nearby chests, chest previews, bigger chests, sorting, quick stack, restocking, routing and multi-user chests. Required on all peers. |
| [Cairn](Cairn/) | 0.1.16 | Meadworks exploration: exploration radius, Exploration skill, per-effect fog controls, time/weather/compass HUD, map marker choices and tidy pins. Imports Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud settings. Client-optional. |
| [Delve](Delve/) | 0.1.11 | Meadworks world regeneration: dungeon sizes, chest refills, dungeon resources, the Queen and camp creatures come back on timers, never near players or their buildings. Server-only. |
| [Edge](Edge/) | 0.2.11 | Meadworks combat: weapon-family secondary attacks, quickstep, slope-aware swings, swim equipment recovery and Dodge Cancel (off by default). Required on all peers. |
| [Ember](Ember/) | 0.5.6 | Meadworks home comforts: faster nights when some players sleep, torches that warm like a campfire, doors that open as you walk up. Required on all peers. |
| [Hex](Hex/) | 0.1.16 | Meadworks cheat toolkit: personal powers, item and creature spawning, skills, map tools, ESP and presets, with server-controlled permissions. Client-optional. |
| [Inscribe](Inscribe/) | 0.2.4 | Meadworks gear inscriptions: item rarity, inscriptions, runes, gems and sockets. Old enhancement levels and scrolls convert to Honing Runes. Required on all peers. |
| [Keel](Keel/) | 0.1.13 | Meadworks ships: tune every ship, grow a Sailing skill with Wind Call and Lookout, and build fast, cargo and livestock ships from vanilla hulls. Required on all peers. |
| [Kindle](Kindle/) | 0.1.22 | Meadworks crafting: recipe search, sorting, favorites, list/grid views, batch crafting, recipe tracking, salvage and Cart Workbench. Client-optional. |
| [Larder](Larder/) | 0.2.12 | Meadworks food and kitchen: food and mead values, eating, regeneration, stamina costs, kitchen stations and Cooking perks. Required on all peers. |
| [Magpie](Magpie/) | 0.1.12 | Meadworks loot handling: magnet pickup, ground-stack merging, floating drops and a server-controlled loot multiplier cheat. Required on all peers. |
| [Mantle](Mantle/) | 0.1.23 | Meadworks inventory and equipment: configurable grid, gear, ring and belt slots, backpacks, key ring, tacklebox and a Swap loadout button. Required on all peers (same version). |
| [Saga](Saga/) | 0.1.14 | Meadworks progression: boss kills turn the Forsaken powers into permanent passives, Defense, Sneak, Woodcutting, Mining, Foraging and Fishing gain perks, and one skill book shows every skill. Required on all peers. |
| [Shepherd](Shepherd/) | 0.1.15 | Meadworks animal husbandry: feed troughs, the Husbandry skill, breeding with sires, star inheritance, twins, herd limits and lineage. Required on all peers. |
| [Tempest](Tempest/) | 0.1.12 | Meadworks weather: rare blizzards and severe thunderstorms with a five-minute warning, the same for every player, plus a server weather filter. Required on all peers. |
| [Thing](Thing/) | 0.1.10 | Meadworks server administration: moderation, messages, world tools and in-game server config editing, checked against the server's admin list. Required on all peers. |
| [Threshold](Threshold/) | 0.3.10 | Meadworks portals: pick any portal from a list and the map, named hubs, access modes, favorites, optional fuel, quick jumps and sea gates for ships (off by default). Required on all peers. |
| [Timber](Timber/) | 0.2.1 | Meadworks building: build camera, flooring options and area repair, with server-controlled cheats. Required on all peers. |
| [Wayfarer](Wayfarer/) | 0.5.12 | Meadworks map travel: Back to Bed for everyone, plus Stones, Anywhere, Boat, Grave and Player travel cheats the server switches on, usable by admins unless the server allows others. Required on all peers. |
| [Wildblood](Wildblood/) | 0.5.4 | Meadworks wild creatures: difficulty and stars that grow with the world, mutations and modifiers, boss aspects, loot rules and bad-luck protection. Required on all peers. |
| [Wildroot](Wildroot/) | 0.1.18 | Meadworks gardening: grid planting, snapping, bulk harvest, replanting, a Farming skill with perks, seed crafting and more plantable flora. Required on all peers. |

## Building

See [BUILDING.md](BUILDING.md).

## Licenses

Each folder carries its own license files. Original code is GPL-3.0-or-later unless the folder's `LICENSE_SCOPE.md`
states otherwise; original artwork and documentation are CC BY-NC-SA 4.0. Adapted third-party code keeps its notices
in the folder's `THIRD_PARTY_NOTICES/`.
