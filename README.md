# Meadworks

Meadworks is a suite of Valheim mods by expDARE. Each folder holds the release source of one Thunderstore package
in the `expDARE` namespace; its README describes features, requirements and configuration.

Crucible is the shared library every product requires. The Meadworks modpack depends on all products.

| Mod | Version | Description |
| --- | --- | --- |
| [Crucible](meadworks-crucible/) | 0.3.33 | Meadworks shared library: product registration, server authority, the shared interface kit, localization and logging. Adds no gameplay. Required on all peers. |
| [Meadworks](MeadworksModpack/) | 0.1.14 | Meadworks suite: Crucible and all 26 Meadworks products in one install. Contains no code of its own. Required on all peers. |
| [Almanac](Almanac/) | 0.1.24 | Meadworks settings book: one in-game window for every installed mod's settings, showing which ones the server locks and why, with settings imports. Client-optional. |
| [Barter](Barter/) | 0.1.4 | ExtraPotions Meadworks trader overhaul: per-trader buy and sell lists, custom currencies, a trader purse that moves prices, and a sell list. Required on all peers. |
| [Beacon](Beacon/) | 0.1.14 | Meadworks saved servers: join without the password prompt, passwords in protected storage, saved servers in Join Game favorites, optional auto-join and reconnect (a server can turn reconnect off), AutoConnector import. Client-optional. |
| [Bestiary](Bestiary/) | 0.1.19 | Meadworks creatures and bone arsenal: Crypt Mimic, Greydwarf Slinger, Rime Giant, Kraken, the arsenal skeletons, the Skeleton Crossbowman and the Crypt Executioner. Required on all peers. |
| [Burrow](Burrow/) | 0.3.29 | Meadworks storage: craft and build from nearby chests, chest previews, bigger chests, sorting, quick stack, restocking, routing and multi-user chests. Required on all peers. |
| [Cairn](Cairn/) | 0.1.18 | Meadworks exploration: exploration radius, Exploration skill, per-effect fog controls, time/weather/compass HUD, map marker choices and tidy pins. Imports Pathfinder, NoFogBruh, BetterFog and TimeWeatherHud settings. Client-optional. |
| [Delve](Delve/) | 0.1.15 | Meadworks world regeneration: dungeon sizes, chest refills, dungeon resources, the Queen and camp creatures come back on timers, never near players or their buildings. Server-only. |
| [Edge](Edge/) | 0.2.12 | Meadworks combat: weapon-family secondary attacks, quickstep, slope-aware swings, swim equipment recovery and Dodge Cancel (off by default). Required on all peers. |
| [Ember](Ember/) | 0.5.10 | Meadworks home comforts: faster nights when some players sleep, torches that warm like a campfire, doors that open as you walk up. Required on all peers. |
| [Hex](Hex/) | 0.1.18 | Meadworks cheat toolkit: personal powers, item and creature spawning, skills, map tools, ESP and presets, with server-controlled permissions. Client-optional. |
| [Inscribe](Inscribe/) | 0.2.5 | Meadworks gear inscriptions: item rarity, inscriptions, runes, gems and sockets. Old enhancement levels and scrolls convert to Honing Runes. Required on all peers. |
| [Keel](Keel/) | 0.1.15 | Meadworks ships: tune every ship, grow a Sailing skill with Wind Call and Lookout, and build fast, cargo and livestock ships from vanilla hulls. Required on all peers. |
| [Kindle](Kindle/) | 0.1.25 | Meadworks crafting: recipe search, sorting, favorites, list/grid views, batch crafting, recipe tracking, salvage and Cart Workbench. Client-optional. |
| [Larder](Larder/) | 0.2.16 | Meadworks food and kitchen: food and mead values, eating, regeneration, stamina costs, kitchen stations and Cooking perks. Required on all peers. |
| [Magpie](Magpie/) | 0.1.15 | Meadworks loot handling: magnet pickup, ground-stack merging, floating drops and a server-controlled loot multiplier cheat. Required on all peers. |
| [Mantle](Mantle/) | 0.1.28 | Meadworks inventory and equipment: configurable grid, gear, ring and belt slots, backpacks, key ring, tacklebox and a Swap loadout button. Required on all peers (same version). |
| [Saga](Saga/) | 0.1.17 | Meadworks progression: boss kills turn the Forsaken powers into permanent passives, Defense, Sneak, Woodcutting, Mining, Foraging and Fishing gain perks, and one skill book shows every skill. Required on all peers. |
| [Shepherd](Shepherd/) | 0.1.21 | Meadworks animal husbandry: feed troughs, the Husbandry skill, breeding with sires, star inheritance, twins, herd limits and lineage. Required on all peers. |
| [Solstice](Solstice/) | 0.1.5 | Meadworks seasons: spring, summer, fall and winter by the world calendar, with seasonal growth, bees, food, stamina, fire, sap, rest and wind effects, and local winter snow and season colors. Cooperates with Tempest weather. Required on all peers. |
| [Tempest](Tempest/) | 0.1.15 | Meadworks weather: rare blizzards and severe thunderstorms with a five-minute warning, the same for every player, plus a server weather filter. Required on all peers. |
| [Thing](Thing/) | 0.1.14 | Meadworks server administration: moderation, messages, world tools and in-game server config editing, checked against the server's admin list. Required on all peers. |
| [Threshold](Threshold/) | 0.3.10 | Meadworks portals: pick any portal from a list and the map, named hubs, access modes, favorites, optional fuel, quick jumps and sea gates for ships (off by default). Required on all peers. |
| [Timber](Timber/) | 0.2.2 | Meadworks building: build camera, flooring options and area repair, with server-controlled cheats. Required on all peers. |
| [Wayfarer](Wayfarer/) | 0.5.13 | Meadworks map travel: Back to Bed for everyone, plus Stones, Anywhere, Boat, Grave and Player travel cheats the server switches on, usable by admins unless the server allows others. Required on all peers. |
| [Wildblood](Wildblood/) | 0.5.7 | Meadworks wild creatures: difficulty and stars that grow with the world, mutations and modifiers, boss aspects, loot rules and bad-luck protection. Required on all peers. |
| [Wildroot](Wildroot/) | 0.1.21 | Meadworks gardening: grid planting, snapping, bulk harvest, replanting, a Farming skill with perks, seed crafting and more plantable flora. Required on all peers. |

## Building

See [BUILDING.md](BUILDING.md).

## Licenses

Each folder carries its own license files. Original code is GPL-3.0-or-later unless the folder's `LICENSE_SCOPE.md`
states otherwise; original artwork and documentation are CC BY-NC-SA 4.0. Adapted third-party code keeps its notices
in the folder's `THIRD_PARTY_NOTICES/`.

## Downloads

[Latest release](https://github.com/ExtraPotions/Meadworks/releases/latest) | [Windows installer](https://github.com/ExtraPotions/Meadworks/releases/download/release-8.9/MeadWorks-Setup-20261025.exe) | [Complete corresponding source](https://github.com/ExtraPotions/Meadworks/releases/download/release-8.9/CorrespondingSource.zip) | [Asset hashes](https://github.com/ExtraPotions/Meadworks/releases/download/release-8.9/SHA256SUMS.txt)

Release8.9 updates ten packages and carries seventeen packages forward byte-for-byte from8.8. Installer SHA256: `ADBF947D55AD3CC546E539CC258F060E544B82CCE67EA6B62B24E6446738F606`. Complete source SHA256: `3902DCA4D695BDC7EFA7F2B5805430EA6EF1684F3C89749B40B56786278A8B27`.

## Changes in 8.9

- Burrow0.3.29 reuses stable container snapshots and guards storage hotkeys during keyboard focus or IME composition, with headless-safe focus checks and a targeted YamlFileWatcher shutdown ThreadAbort catch that preserves ordinary error reporting.
- Kindle0.1.25 avoids redundant Repair All tooltip writes and refreshes affected data when locale/catalog inputs change. Its crafting/refund safeguards shipped earlier.
- Ember0.5.10 and Saga0.1.17 add Essential tier resources limited to twelve entries and version metadata; their existing domain preset logic is unchanged.
- Solstice0.1.5 adds a twelve-setting Essential list. Almanac0.1.24 supplies the central catalog and season presentation bundles for existing settings, without new Solstice gameplay runtime.
- Crucible0.3.33/UI0.3.22 supplies shared preset APIs, native notice wrapping and registered local-clause preset lookup. Thing0.1.14 adds selected-player identity to both Sub-Admin confirmations in all seven languages.

The release notes separate already-shipped8.8 behavior from new8.9 changes. Actual100-key/34local/six host checks, nine cold and nine update scenarios passed, with strict mod/unknown-console findings zero. One existing profile/60DLLs and all27source inventories were verified after backup/update, preserving personal settings, saves, enabled states and unrelated mods. Final installer/source artifact readback passed. Coverage remains focused; full physical multiplayer and arbitrary-mod compatibility are not claimed.
