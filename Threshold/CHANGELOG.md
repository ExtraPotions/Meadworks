# Changelog

## 0.3.12 (2026-10-28)

- After a portal trip starts, Threshold verifies the fuel removal. A fuel-removal error no longer prevents arrival protection from being set up.

## 0.3.11 (2026-10-26)

- If map pins fail to draw, they stay off until the large map is closed instead of retrying and logging every frame.

## 0.3.10 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- German, Russian, Chinese and Korean portal text uses the game's stone-portal and cart names.
- A sea-gate teleport the game refuses no longer drops that crew member off the helm or uses up the jump. The ship is told they are not coming.
- Only the player who is landing, or the server, can mark sea-gate crew as landed. Only the owner of the other pillar, or the server, can clear a saved sea-gate link.
- Portal pins on the large map lay out once, and skip that work while the pins, the map view and spread are unchanged. The destination picker reuses its gamepad button list until the rows change.

## 0.3.9 (2026-10-20)

- Requires Crucible 0.3.24 / Crucible.UI 0.3.17.
- Portal map (4C-11): while you choose a destination, "You are here" marks the portal you walked into (shown faded in its color). With Show Portal Pins on, the open large map labels your own position the same way.
- Double-click the map while choosing a destination to place an ordinary map pin; right-click a map pin to remove it. These are the game's own pins, saved with your character for that world, so other mods and the minimap see them as usual. A single click on the map still does nothing while you choose.
- A click on a portal icon now travels after the game's 0.3 s double-click window; double-clicking a portal icon places a pin there instead of traveling. Right-clicking a portal icon removes a map pin under it.
- The destination list shows a short hint for map pins. New text in English, Spanish and French.
- Adapted from Wayfare's MapClicks (GPL-3.0-or-later, pin ad278ecf); recorded in docs/source-provenance.json.

## 0.3.8 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Simpler in-game settings: the MeadWorks menu (F8) shows Threshold's seven essential settings; everything else stays in Almanac and the config file, and prefab/item lists, the pillar recipe and the lock lists are marked as file settings. The separate "Portal pins on/off" F8 entry is gone because Show Portal Pins is now one of the essential settings there. Portal Pins Key (was P) is unbound by default; a saved P moves to unbound once (the cfg is backed up first) and any other key you chose is kept. No setting keys change.

### Reference parity

- New host setting 7 - Jumps / Arrival Protection Seconds (default 3, 0 turns it off): after a portal trip you take no damage and creatures do not target you for a few seconds. Creatures owned by a machine without Threshold can still target you; damage stays blocked.
- New setting 4 - Picker / Highlight Stone Portals (on by default): portals that let every item through get their own Stone portals group in the list and gray pins on the travel map and the large map.
- New key 5 - Map / Portal Pins Key (default P): on the open large map, shows or hides the portal pins. Clear it to have no key.
- New host setting 7 - Jumps / Bring Carts (on by default): on a trip Threshold routes, the cart you pull comes through the portal with its load and stands in front of you on arrival. The load is moved with the cart, never copied. A cart carrying ores or other restricted items follows 1 - General / Ore Transport (by default only from a stone portal); otherwise the trip is refused. A cart another player has open or owns stays where it is. Left to TeleportEverything when it is installed.

### Batch 4D settings text

- Picker / Sort lists its choices, and General / Teleport Mode explains Default mode in plain words.

## 0.3.7 - batch 4E follow-up (2026-10-19)

- 1 - General / Locked Player Settings now works: settings listed there use the server value for connected players.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.3.6 (2026-10-18)

- Use shared dialog and text-entry boundaries for existing portal controls while preserving travel validation.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

- Refuse routed travel when fuel is enabled but its configured item prefab is unavailable.
- Add server-controlled Ore Transport modes: Blocked, StoneToStone, FromStoneToWood and All. FromStoneToWood keeps the existing default.
- Recheck a clicked destination against the current server portal list before applying its item allowance.

## 0.3.5 (2026-10-16)

- Rebuild the portal destination picker after leaving and rejoining a world.

## 0.3.4 (2026-10-15)

- First release on Thunderstore.
