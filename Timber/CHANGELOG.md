## 0.2.2

- Reject missing item or shared records before camera pickup eligibility, ownership and inventory checks.
- Recheck item records after loading them while preserving valid pickup rules.

# Changelog

## 0.2.1 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- German, Russian and Korean build-camera text uses the game's gamepad word. The cheat section title matches the other mods.

## 0.2.0 (2026-10-19)

- Update required to join servers running this version: players on older versions must update first.
- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Blueprint removal

- Removed: blueprint/planning features (permanently excluded): blueprint library and Blueprints tab, construction sites and deliveries, copy building, fix ground, site planner, `timber blueprint` console commands, their settings, keys, texts and OpenKeep assets. `timber import openkeep` skips OpenKeep's blueprint settings. Worlds with old construction posts still load; the posts no longer appear.

### Batch 4D settings text

- Build camera invert settings and the Flooring settings (Floors Are Roofs, Smelters On Floors, Floors Dont Decay) explain their effect in plain words.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.

### Simpler in-game settings

- Added settings tiers: the per-mod menu shows six essential settings (Build Camera Enabled, Camera Pickup, Toggle Key and Speed; Area Repair; Fires On Floors). Almanac still lists every setting.
- Circlet Offset and the two Server lock lists are marked as normally edited in the config file. No keys, defaults or hotkeys changed.

## 0.1.10 (2026-10-18)

- Use shared owned HUD layout and presentation controls for the existing camera display.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

- Added default-off Repair / Area Repair: a successful hammer repair also repairs up to 64 eligible direct touching pieces, retaining native station/ward checks and one-swing costs. Focused local native qualification passed all eleven controls.

## 0.1.9 (2026-10-15)

- First release on Thunderstore.
