# Changelog

## 0.1.17 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- README: the Thing grants line names the World tools grant and the Spawn grant.

## 0.1.16 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Debug kill, remove drops and tame take a radius from 5 to 1000 meters. Tame affects wild creatures in that radius. Stop event ends the current event and does not use the radius. Kill still skips players and tames.
- Routed debug kill, drops, tame, and stop event, and spawn, accept a Thing per-player grant when the only mischief refusal is that the player is not an admin. Nobody and any other block still refuse. If Thing is not installed, this behavior is unchanged.

## 0.1.15 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- License text: LICENSE-ASSETS.md now says Hex ships its approved original icon.png (CC BY-NC-SA 4.0) instead of "no image".

## 0.1.14 (2026-10-20)

- Requires Crucible 0.3.23 / Crucible.UI 0.3.16.
- Faster inventory with no carry limit: when another mod (Mantle) already writes the weight text, Hex no longer writes its "x/∞" first, so the label is not changed twice every frame. No visible change.

## 0.1.13 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Simpler in-game settings

- Simpler in-game settings: the Hex window has seven pages instead of eleven. Fun and Speeds are one Movement page; Items and Creatures are one Spawn page with an Items/Creatures switch (the Spawning switch now lives only there); World seed moved to the Map page and opens with Map Tools or Debug Tools; the Midnight/Sunrise/Noon/Sunset buttons sit inside the Local Time row.
- Moved to Almanac (no longer repeated in the Hex window): the Achievements toggles, Menu Key and Panic Key, the server cheat policy (Crucible Mischief settings), ESP search/distance/filters and every per-row Hotkey button. All actions, cheats, presets and server permission checks work as before; the `hex` console command is unchanged.
- Ships settings-tiers.json: the MeadWorks menu shows Enabled, Menu Key, Panic Key, God Mode, Fly, Unlimited Stamina, No Carry Limit and Keep Items On Death; boss-power, numeric, page-switch and achievement-mark hotkeys are marked as normally edited in the config file. Config keys, values and defaults are unchanged.

### Deep North fixes

- Points of interest: the Deep North boss room (DN_Bossroom) is labeled with the Frozen King's game name ($enemy_frozenking, Kall Fimbulbringer) instead of a missing token.
- Read existing text and indicator colors from shared Crucible.UI color roles; colors are unchanged.

## 0.1.12 - batch 4E gameplay fixes (2026-10-19)

- Cheats honor the admin "Players may use" switch, so admins can let connected players use a cheat; otherwise it stays admin-only and shows locked.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.11 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.10 (2026-10-18)

- Use shared presentation and control hover explanations in existing tools while preserving action validation.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

- Added a default-off spawn protection terrain overlay to Debug, `hex protection` and an optional unassigned action shortcut. It uses actual loaded terrain triangles and PlayerBase collider radii, samples native structural spawn blocking, and retains thin gaps between protection areas. Toggle off, Panic, module/master disable and session loss release its owned rendering objects. Debug Tools and Information permission remain required; the overlay is excluded from presets.
- Focused local native checks passed on the recorded candidate. Terrain fragments remain approximately 8 centimeters above the native surface. Remote grant revocation, physical input and visual acceptance of the menu remain unqualified.

## 0.1.9 (2026-10-15)

- First release on Thunderstore.
