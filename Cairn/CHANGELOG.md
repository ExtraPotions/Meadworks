# Changelog

## 0.1.22 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.1.20 (2026-10-26)

- The HUD compass no longer hides and re-shows every tick, mark and marker on each redraw, which caused stutter while turning the camera.
- Tidy map pins update a pin only when its look changes, instead of restoring and re-applying every pin every frame.

## 0.1.21 (2026-10-28)

- New package icon.

## 0.1.17 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- README: the forecast description says seasonal names come from Solstice, and that without Solstice (or with its seasons off) the line uses the game's own weather.

## 0.1.16 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- The clock line shows the next weather for the biome you are in: its name, the minutes until the shared world clock changes it, and the wind direction and strength at that change. The roll is the game's environment schedule, so a client matches the host. When Tempest is installed, environments it has switched off are left out. `4 - HUD / Show Forecast` (your own setting, default on) hides it. The line stays hidden while a raid, a forced environment or a Tempest event is controlling the weather. The current weather stays beside the forecast when both fit, and the clock is never cut.
- Seasonal weather from Solstice, such as Clear Winter, shows the same name and icon as the weather it copies. The forecast still uses the current season's weather list, so a change after the season turns can differ.
- New texts in English, Spanish, French, German, Russian, Chinese and Korean.
- The weather forecast looks up Tempest once per session. With Tempest not installed, the HUD no longer scans loaded assemblies every frame.
- Spanish and French marker-map help, and the other compass-window help lines that were still in English, are translated.

## 0.1.15 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- License text: LICENSE-CODE.md now lists all four adapted sources, adding BetterSleepBruh 2.1.0 (MIT) as LICENSE_SCOPE.md and SOURCE_NOTICE.md already did.

## 0.1.14 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Opening the inventory or crafting window no longer rewraps the status effects. The compass hides under the window, but the row keeps the wrap it had until the window closes, so the icons stay put. The same applies under the Esc menu, the large map and MeadWorks windows, and matches Mantle's hold.

## 0.1.13 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
### Playtest fixes (2026-10-20)

- The game's status effects no longer run under the compass. While the compass shows and shares their height, the row wraps to a new line before it reaches the plank, using the game's own spacing. With the compass hidden or Cairn stopped, the game's layout returns. This mainly happens when Mantle moves the row left for its stat column.

## 0.1.12 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- Cairn's settings are edited in Almanac. The Cairn window's HUD, Compass, Visibility and Map tabs are removed; the window keeps only the compass markers tool (pin types, single pins, marker map, Map cursor).
- The MeadWorks menu entry "HUD settings" is now "Compass markers" and opens that tool. "Cairn: HUD on/off" is unchanged. Settings Key and Gamepad Settings open the same tool.
- The HUD no longer draws the radius inputs breakdown. `Show Radius Inputs` stays in the config file as a diagnostic setting; Show Radius is unchanged.
- Settings are tagged Essential (8), Advanced or File-only (19) for the MeadWorks menu and Almanac. No setting or config key was renamed or removed; saved values are kept.
- New texts in English, Spanish and French.

### Graded fog

- New section `9 - Visibility Strength` (your own settings, default 1 = the game's look, range 0-1, applied at once): Distance Fog Strength scales fog density, Mist Strength scales mist and fog particle opacity (Mistlands mist, fog banks, weather fog), Smoke Strength scales fire smoke. A Remove toggle still wins; a server's Allow Removing off keeps that effect at full strength, and Mist Strength is held at 1 on servers without Cairn. Adapted from BetterFog (MIT, notice retained).
- Importing a BetterFog config now carries graded values (between 0 and 1) into these settings instead of only noting them.
- New texts in English, Spanish and French.

### Map pins and compass width

- Tidy Pins: hold Left Alt on the large map to show every pin name (`7 - Map / Show All Names Key`, rebindable; a BepInEx key setting, so MeadWorks key-clash warnings cover it).
- Tidy Pins: player, ping and shout pins keep the game's size and opacity.
- Tidy Pins: the pin under the mouse or gamepad cursor grows (`7 - Map / Hovered Pin Scale`, default 1.25, 1 turns it off).
- New HUD setting Compass Strip Width (default 240, the current width; range 240-640). It applies live and the time line, compass marks and pins are placed again for the new width.
- New texts in English, Spanish and French. German, Russian, Chinese and Korean show English for them (language freeze).

### Batch 4B weather events

- The Tempest row shows Lightning Storm, Dense Fog, Ash Storm and Meteor Shower by name with their own glyph (bolt, cloud, star). It reads Tempest's detail capability and falls back to the older one, so older Tempest builds still show blizzards and thunderstorms.
- Tempest's new weather shows the right weather mark: Lightning Storm as a thunderstorm, Dense Fog as mist, Ash Storm and Meteor Shower as ash.
- During a Tempest event the top row names the event as the Tempest row and badge do (Lightning Storm, Severe Thunderstorm, Blizzard, Dense Fog, Ash Storm, Meteor Shower) with its glyph, instead of Thunderstorm, Mist or Ash rain. It reads Tempest's environment names and active event, so Cairn still runs without Tempest; indoors and boss arenas keep their own name.
- A long weather or event name on the top row (Lightning Storm, Tormenta eléctrica fuerte, Pluie de météores) now fits beside the clock: its text shrinks (down to size 8) and only ends in an ellipsis if it still does not fit. The clock is never cut, and the plank keeps its width.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.

## 0.1.11 - batch 4E gameplay fixes (2026-10-19)

- New server setting World Time Speed (default 1, range 0.1-10) changes the shared world clock, including day/night, crops and timers. It applies live and keeps the current time of day; vanilla players can still join.
- Settings audit: affected settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.10 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.9 (2026-10-18)

- Keep the status HUD visible while the HUD layout editor is open; ordinary blocking windows still hide it.

- Keep HUD visibility available while the shared HUD layout editor is open, while preserving normal window visibility rules. Shared layout overrides retain automatic placement and can return to it without changing Cairn's enabled settings.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.1.8 (2026-10-15)

- First release on Thunderstore.
