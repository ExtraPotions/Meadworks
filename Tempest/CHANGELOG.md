# Changelog

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
