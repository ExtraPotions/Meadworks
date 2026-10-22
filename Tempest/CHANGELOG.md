# Changelog

## 0.1.15 (2026-10-22)

- Requires Crucible 0.3.30 / Crucible.UI 0.3.21.
- 0.1.15.
- The weather filter rebuilds its disabled-environment list when that setting or the season changes. A settings read that saw the empty default no longer leaves the environment list unfiltered.

## 0.1.14 (2026-10-22)

- Requires Crucible 0.3.29 / Crucible.UI 0.3.21.
- 0.1.14. Seasonal weather weights still apply after Solstice's seasonal weather rules now that Solstice's plugin ID is `com.meadworks.solstice` (Solstice 0.1.3). The older ID `com.extrapotions.solstice` is still recognized for one release, so Tempest keeps the same order with Solstice 0.1.2.

## 0.1.13 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.12 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Optional Solstice integration: seasonal environments follow base weather switches; four host-owned seasonal weight lists apply live with neutral defaults. Event warning and clearing weather can use existing seasonal variants while active Tempest event skies retain ownership. No Solstice dependency is required. See docs/SEASONS.md for the contract and Cairn forecast display limits.
- German, Russian, Chinese and Korean weather text quotes setting names as names. The shield generator uses the game's piece name.
- The weather badge updates its title and countdown when those lines change, including once a second while time is left. It no longer joins them into one string every frame. The meter still moves every frame.

## 0.1.11 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- License text: LICENSE_SCOPE.md now says Tempest ships its approved original icon.png (CC BY-NC-SA 4.0) instead of "no image".

## 0.1.10 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Settings simplification

- Simpler in-game settings: Tempest now marks 8 settings as essential (events on or off, blizzard cold, strike and storm hazard switches, and the three notice switches), so the MeadWorks menu can show just those. The biome lists, the boss, dungeon and raid weather switches, Disabled Environments and the product-managed records are marked as config-file settings. Every setting keeps its key, value and scope, and Almanac still lists all of them.
- The `wildweather` console alias is gone; use `tempest` with the same verbs.
- When WildWeather, NoFogBruh or Valheim_Admin_Tool is installed, Tempest no longer shows a center message per module; it writes one summary line to the log and records each module for the Crucible diagnostics command.
- The NoFogBruh import stays in Almanac's Convert tools and never appears in a mod window or the F8 page. It can now also run from the console: `tempest import` previews it (and writes `config/Meadworks/tempest/import-nofogbruh.txt`), `tempest import apply` imports it after backing up Tempest's settings file. Host or single player only.

### Batch 4D settings text

- Clearer setting text for the ash rain and fog-free thunderstorm weather switches.

### Batch 4B weather notices

- While a player window is open (inventory, containers, crafting, skills, Compendium, map, pause menu, trader, MeadWorks windows), the weather badge is hidden and weather messages go to the message log only. Nothing is drawn over the window.
- Standing in a strike's lingering ground lightning shocks the player once a second (the game's Lightning effect plus lightning damage, armor and resistances apply) until the strike fades or the storm ends. New host settings: Storm Hazards Affect Players (on) and Storm Hazard Damage (5).
- Strike Starts Fires now works when it is not raining: half of the strike's damage is fire, the strike sets a tree or dry grass alight with the game's own fire (never inside wards, shields or base clearance, never player-built pieces), and the strike's ground lightning also applies the game's Burning effect with Storm Hazard Damage split evenly between lightning and fire. Rain cancels the fire effect.
- Lightning Storms: a new thunderstorm becomes a Lightning Storm with the host's Lightning Storm Chance (default 5%). It has more lightning (Strikes Per Minute times the host's Lightning Storm Strike Multiplier, default 2) and no rain, so Strike Starts Fires can set things alight. Its name reads "Lightning Storm". Admins can summon one with `tempest storm lightning` (`dry` also works; add `now` to skip the warning). Older clients see a normal thunderstorm, and a Lightning Storm summoned on an older server falls back to a normal thunderstorm with a message.
- When Cairn's weather display is on, Tempest no longer shows its own center weather message as well.
- New weather events: Dense Fog (Meadows, Black Forest, Swamp, Plains, Ocean; sight drops to about 15 to 20 m, no damage), Ash Storm (Ashlands; players out in the open burn every 5 seconds, a roof or interior protects) and Meteor Shower (Plains and Ashlands, always at night; meteors land like strikes and follow the strike, fire, ward and base rules). Each has a host chance (0.04, 0.05, 0.03) and biome setting.
- Lightning Storm is now its own event kind with its own name and environment; Lightning Storm Chance and Lightning Storm Strike Multiplier keep their meaning and defaults.
- The daily roll now gives each event exactly its own chance; default Blizzard and Thunderstorm odds are unchanged.
- Admin commands `tempest lightningstorm`, `fog`, `ashstorm` and `meteorshower`, each with `[now]`.
- Older clients keep working: they see a Lightning Storm as a thunderstorm and the new events as no event. Old saves load unchanged.
- Cairn and other HUDs can read every event's name through the new `meadworks.weather.events.detail` capability; `meadworks.weather.events` 1.0 is unchanged.

## 0.1.9 - batch 4E gameplay fixes (2026-10-19)

- Ground lightning effects and sounds are cleared as soon as a storm ends; late strikes after the storm are ignored.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.8 (2026-10-18)

- Use the shared owned HUD layout and presentation controls for the existing weather display.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.7 (2026-10-15)

- First release on Thunderstore.
