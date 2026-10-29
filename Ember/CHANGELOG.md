## 0.5.13 (2026-10-28)

- Dependencies updated to the latest versions (Crucible 0.3.37, BepInExPack_Valheim 5.4.2360, Jotunn 2.30.2).

## 0.5.12 (2026-10-28)

- New package icon.

## 0.5.11 (2026-10-26)

- Placement ghosts of fireplaces and doors are no longer tracked as real pieces.

## 0.5.9

- Keep automatic door checks at 0.05 seconds while reusing scan collections and handling callback mutation and shutdown safely.
- Avoid an intermediate dead-fireplace cleanup list when no tracked entries have died.
- Guard torch keep, schedule and hover paths when network state is unavailable.
- Reject missing torch shared data before durability calculations.

# Changelog

## 0.5.8 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20 (Crucible 0.3.27 on Thunderstore is a source archive without DLLs).

## 0.5.7 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- README: the Seasons description now gives Night Length's limits and requirements, Fuel Burn's range and what Torch Heat does.

## 0.5.6 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Optional Solstice integration uses the host's seasonal facts for bounded night length, held-torch durability drain and torch warmth. Sleep skips, partial-sleep caps and night-only torches follow the seasonal dawn; world day length and day counting stay unchanged. Missing or disabled seasons preserves Ember's existing behavior. Placed-fire seasonal fuel remains Solstice's rule and is not applied twice.
- Added three live, server-owned Seasons switches with metadata in all seven languages. Host facts are synchronized for the current world and connection; client preferences stay saved locally. Existing sleep and door presets keep seasonal choices unchanged, and fuel-free/endless behavior keeps priority.

- Sleep timer protection applies only to boost accrued while each timer setting is enabled, including unloaded crops, regrowing pickables and ongoing mead batches. Starting a new mead batch discards compensation accumulated while its fermenter was empty.
- The sleep HUD reuses unchanged captions and reads chevron states without creating arrays; its appearance and animation are preserved.

- Sleep and automatic doors each have an Essential preset selector: MeadWorksDefault, Vanilla, Relaxed and Custom. Selecting a preset applies its values live; changing a member selects Custom. Existing customized values and the one-time untouched door-distance migration are preserved.
- Presets respect host authority and locked door settings. Local preset saves preserve server overlays and unrelated saved preferences. Added preset descriptions in all seven supported languages.

## 0.5.5 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- Automatic doors close after you walk away even when you leave while the door is still swinging open. Before, leaving within the opening animation (easy with the shorter distances) left the door open.
- New Sleep / Sleep In Any Bed (host decides, on by default): sleep in a bed another player has claimed. The game ignored such beds entirely; now the hover shows the sleep prompt and the game's time, enemy, roof, fire and wet checks apply. The bed keeps its owner and your spawn point does not change.
- Doors: Open Distance and Close Distance defaults are halved: doors 1.5 / 2.25 m (were 3 / 4.5 m), drawbridges 3 / 4 m (were 6 / 8 m). On first start a saved distance that still equals its earlier default moves to the new default once, with one log line; customized distances are kept.

## 0.5.4 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- Simpler in-game settings: ships settings-tiers.json, so the MeadWorks menu shows Ember's 8 everyday settings (Sleep on/off, nearest-bed respawn, sleep HUD, torches, night-only torches, torch switch key, auto doors and their toggle key); everything else stays in Almanac, and testing, fire-piece lists, distances of never-opening doors and server/import entries are marked as normally edited in the config file. Config keys, values and defaults are unchanged.

### Batch 4D settings text

- Deep North (Valheim 1.0.20): Buildable: Drawbridge is the game's Timberwood Drawbridge (piece token $piece_drawbridge_dn); new World: Morkhalla Drawbridge section (drawbridge distances, does not open by itself by default); held torches burn in the Deep North snowstorm (Twilight_SnowStorm).
- Clearer setting text for Sleep / Bonus Increment Scale and Torches / Held Torch Warms.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.

## 0.5.3 - batch 4E gameplay fixes (2026-10-19)

- Settings audit: affected settings apply live and report their owner.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.5.2 (2026-10-18)

- Keep the sleep HUD visible while the HUD layout editor is open; preserve existing sleep and window behavior.

- Keep the sleep HUD available to the shared HUD layout editor while preserving normal menu, map and inventory visibility rules. Layout overrides do not change sleep or respawn behavior.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12 for the shared UI/API integration. Broader 4D qualification is pending.

## 0.5.1 (2026-10-17)

- Increase normal local-player interaction reach to 1.5 times the native range without changing seated-control detachment or object-specific interaction checks.
- Ignore stale enemies-nearby bed warnings after a threat dies, despawns, or becomes friendly when local AI evidence is complete. Preserve live threats and uncertain ownership.

- Add native-map death-bed selection in 0.5.0: nearest default, a 30-second choice, native single/double-click coexistence and confirmation that respects Quick Respawn on/off timing. Remote early release requires the actual current bed plus quiet, bounded by the native deadline. Focused local and connected native checks passed; physical input and WAN completeness remain unqualified.

- Add Show Owned Beds on Map in 0.4.0, a local option enabled by default when Nearest Owned Bed Respawn is active. Temporary yellow native bed icons show learned beds in the current world, preserve the bound-bed icon and map filter, and clear owned display state when disabled. Focused single-player native lifecycle/filter/save/re-entry checks and minimap/large-map screenshot inspection passed. Cross-world/character reset has stub coverage; Cairn consumption was inspected in source. Multiplayer and physical input remain unqualified.

- Add default-off Quick Respawn, independent of nearest-bed selection: distance-sensitive death/loading delays, unchanged native bed and terrain validation, and bounded replicated-object quiet checks on remote clients. Disabling preserves an already scheduled request and stops further acceleration. Focused local and dedicated/client checks passed.
- Fail closed when another patch skips, rewrites, nests or throws during the owned respawn reschedule; preserve pending requests and restore only still-owned per-call load thresholds.

- Add default-off Nearest Owned Bed Respawn: remember discovered beds claimed by the current character, choose the nearest on death, and let the game's bed validation fall back past destroyed or reclaimed beds. Character/world records persist; login and first spawn keep their native behavior. Focused single-player native checks passed, including save/re-entry.

- Default to fuel-free, smokeless standard placed torches, campfires and held Torch, preserving stored fuel/durability and native campfire heat.
- Add default-off night scheduling, a separate torch list and margin, and an authenticated keep-lit switch with persistent world flags; native qualification is pending.

- Add default-off Stand Up On Respawn in Sleep; preserve login/first spawn and step aside for OpenKeep's active equivalent.

## 0.1.9 (2026-10-15)

- First release on Thunderstore.
