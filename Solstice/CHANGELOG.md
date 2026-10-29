# Changelog

## 0.1.9 (2026-10-29)

- Package files now carry this release's date, so BepInEx reloads updated mods instead of using stale cached plugin details (fixes 'missing dependency Crucible' after updating).

## 0.1.8 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.1.6 (2026-10-28)

- An interrupted winter harvest resumes its saved item plan. If a drop call throws after it may have created an item, Solstice skips that item on retry to avoid a duplicate.

## 0.1.7 (2026-10-28)

- Defer player-facing season work until the local player is ready while keeping authoritative server season work active whenever the environment is available.

## 0.1.3 (2026-10-22)

- 0.1.3. Solstice is now a MeadWorks plugin everywhere: its plugin ID is `com.meadworks.solstice` (was `com.extrapotions.solstice`). Almanac lists it under Meadworks, and Crucible treats it like every other MeadWorks product.
- Your settings carry over. On the first start, `com.extrapotions.solstice.cfg` is copied to `com.meadworks.solstice.cfg` and the old file is kept as `com.extrapotions.solstice.cfg.bak`; the seasons import marker moves the same way, so an import that was already applied is not applied again. An existing new file is never overwritten. The world's season clock is unchanged.
- A server that let players use Season Override through the admin "Players may use" list must allow it again after the update: that list names the plugin ID, so the old grant no longer matches. Until then Season Override stays admin-only.
- Update every player and the server together: a 0.1.2 client and a 0.1.3 server (or the reverse) do not see the same plugin ID and cannot join.

## 0.1.2 (2026-10-21)

- 0.1.2. One per-frame tick that throws is logged at most once a minute and no longer skips the other ticks.
- Snow-cover pass lines and per-texture source and frost averages, including sky, sun and ambient, are Debug. A snow frame over 50 ms still warns, at most once a minute.
- A day change or a General Enabled toggle logs a Debug breakdown of Solstice's own tick times, and whether the weather list, environment variants, music invalidate, visuals repaint or minimap tint ran in that frame.

## 0.1.1 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.1.0 (2026-10-21)

- First release. Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- New product: Solstice, four seasons by the world calendar (Runestone, required on all peers).
- Calendar: configurable season lengths, server-authoritative.
- Seasonal effects on plant growth, beehives, food, stamina, fire fuel, sap, the Rested effect, wind and rain wear.
- Protected places (indoors, Ashlands, Deep North, high mountains, shield dome) and the winter cold rule for plants with fire heat protection and a hardy crop list, from the original mod.
- Seasonal weather through the biome weather lists, yielding to Tempest and to forced environments.
- The season capability `meadworks.seasons.view` 1.0 for Cairn, Tempest and other mods.
- Season Override cheat, `solstice` console status, season-start message.
- Adapted from Seasons 1.10.6 by shudnal (Unlicense); its textures, snow meshes, walkable ice and music files are not included.
- Icon: a sun, drawn in code in the Meadworks icon style (approved 2026-10-10). Notices and season names in English, Spanish, Simplified Chinese, Russian, French, German and Korean.

Development history:

- Black Forest winter no longer turns firs and ground plants black. The frost copy is read as sRGB, so the shared fir texture and the forest-floor cards keep their color, with a light frost on the green.
- Leaving a world and joining again rebuilds the season plate, the heat meter and the season status. A destroyed HUD object is recreated, and the status name is localized again instead of staying SolsticeSeason.
- Black Forest ground plants take the same light frost as leaves. The frost copy keeps each source alpha byte, so the cutout edges stay the game's.
- Winter snow on this client no longer frosts every texture and rebuilds every terrain chunk in one frame. Each source texture is read once per winter session, a strength step is built off the main thread, and loaded chunks rebuild a few per frame, nearest first. Bark, trunk, log and stem materials stay as they are. Leaves take a light snow and brown pixels stay brown. Open lowland snow stops at 0.8 of the mountain mask. Clear Winter and Heath clear Winter lower the midday sun a little on a graphical client. A narrow heat meter leaves the word off when it would be under 8 pixels.
- Winter ground on this client uses the terrain shader's mountain snow mask on Meadows, Black Forest, Swamp and Plains, rebuilt when the season or the winter day changes. Grass and leaves use a frosted texture copy instead of a white color multiplier, and clutter that only grows in those biomes is thinned. Frozen water no longer writes a surface color and stays icy blue with some transparency. The overheat mark is a flame. The heat label has a dark outline. Winter status keeps the Solstice winter mark when season icons are off.
- Winter Meadows uses snow instead of rain, and those snow copies are not Wet. Snow Cover still raises the game's snow level where a material already has one. The season plate reserves a HUD rectangle so notices step aside, and the heat meter hides when the only gap is the compass band. Goblin king and dragon music names stay reserved. A crop watch attaches only to crops on the server list, and a perish drop is marked picked before the items fall.
- On this client, Solstice can play one of the game's own ambient tracks for the season, draw a season mark on the plate and the season status, and show a heat meter for warm-clothes overheat. Frozen winter days also tint foam and the top water color, when the water has them, and space the ripples. Each one is a local setting and applies at once. There is still no ice you can walk on. Seasons' summer-heat meter is not copied. A dedicated server does not play music or draw these.
- Crop freeze checks follow ownership on every peer and keep the full configured grace through sleep and world-time skips. A failed survival drops the normal harvest once; survival applies only to the current winter.
- Unknown or damaged saved clocks are preserved with seasonal effects off. Beehives, fire fuel and sap integrate each season across unloaded gaps, and a winter override retains earlier plant growth.
- Turning Solstice off restores seasonal skies, material instances and both maps. Late weather sources retry, and Swamp Summer uses the game's Darklands environment.
- All settings and section headings have names and help in all seven languages. Shelter and interiors stop warm-clothes overheating.
- The season is saved with the world (`mw.solstice.clock`) and sent to clients. A world with no clock starts from the same day-to-season math as before. Changing a later season's length no longer moves the season already underway.
- Other mods can read `meadworks.seasons.clock` 1.0 and `meadworks.seasons.facts` 1.0. `meadworks.seasons.view` 1.0 is unchanged. See docs/SEASON-CONTRACT.md.
- Ripe crops on a server list can freeze in a winter that stops growth. Failed survival drops their normal harvest. A fire, a hardy crop, or a protected place keeps them ripe.
- The season status effect uses the existing stat table outdoors, except in the Ashlands and the Deep North. Two or more frost-resistant clothes in a hot season slow stamina and Eitr recovery unless sheltered, indoors, cold, or eating a cooling food.
- Snow cover and frozen water are numbers on the facts contract. Solstice does not spawn snow or ice.
- On this client, grass, leaves, the ground and both maps take a season color. Winter can raise the game's own snow level on materials that already have one, and shallow water can tint toward ice on the frozen days. Nothing is saved onto buildings, and no snow mesh or walkable ice is added. A small plate shows the season and day. These client visuals apply immediately.
- Seasonal weather names such as Clear Winter are cloned from the game's own environments and tinted, so those swaps are no longer skipped.
- A `Solstice.seasons.json` file can fill settings that are still at their defaults. Night, torches, wood, meat and breeding are left for the mods that own them.
