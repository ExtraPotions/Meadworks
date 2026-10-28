## 0.1.16 (2026-10-28)

- Delve now replaces an existing world file in one step, so an interrupted replacement leaves the previous file intact. On the first save, Delve loads the temporary file on the next start if the main file was not created.

## 0.1.15

- Guard world object creation, removal and nearby queries when scene or world-object services are unavailable.

# Changelog

## Unreleased

- Reuse player dungeon lookups within each regeneration sweep. Geometry, radius and player-snapshot changes rebuild the lookup; current generator, door and resource holds are still checked for each site.
- Reject impossible-height dungeon lookups before copying tracked site lists. Entrance, indoor occupancy, regeneration schedules and protection rules keep their existing behavior.
- Add one Debug timing summary per regeneration check, including dawn detection, service and dungeon sweep timings, scan counts, current regeneration reports, and sidecar serialization and disk timings. Diagnostics allocate nothing when Debug logging is off. Regeneration schedules, guards, saves, and action budgets are unchanged.
- Bump the local development version to 0.1.14.

## 0.1.13 (2026-10-21)

- Requires Crucible 0.3.28 / Crucible.UI 0.3.20.
- A full regenerate (`delve-regen`, or an admin request that names every kind) clears Bestiary region claims on the server only after the kinds it accepted have finished. Regenerate Without Waiting has to be on. A request that keeps the usual delays leaves the claims, including when the cheat is off or the Mischief gate locks it. Kinds that are switched off are not part of that regenerate. A request that is refused, canceled, or fails on that check leaves the claims. Dry Run All leaves the claims. Regenerating one kind does not. Finishing the same regenerate again does not clear them a second time.

## 0.1.12 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- A full regenerate (`delve-regen`, or an admin request that names every kind) clears Bestiary region claims on the server. Dry Run All leaves them. Regenerating one kind does not.

## 0.1.11 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- A loot chest that a player has loaded keeps its current items until it unloads. Restock Loaded still lets the delay run while the chest is loaded. The new roll is written on the first check after the loaded copy is gone.
- A flametal spire that drifts keeps one remembered spot. A missing flametal spot is not grown while any whole spire of that ore remains.
- A full dungeon reset generates the replacement before it removes the old interior. If generation fails, the new pieces are removed and the old dungeon stays.

## 0.1.10 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Kall Fimbulbringer returns asleep to his Deep North boss room (DN_Bossroom) after his death and a delay without visitors, like the Queen: new Bosses settings Kall Return (on), Kall Clock, Kall Delay (30 in-game days) and Kall Dry Run, and `delve-regen-kall`. A room with Kall alive in any phase, a player inside or a player holding its objects is never touched. The room has no door; its offering altar is unchanged.
- A citadel whose type is Full in Delve.Dungeons.yml now gets the Queen back in her room when its regenerated rooms do not bring her, and its sealed door locks again while Close Sealed Door is on.

### Simpler in-game settings

- Simpler in-game settings: ships settings-tiers.json, so the MeadWorks menu shows Delve's 8 main switches (Enabled, chest refills and delay, Queen and Kall return, camp and dungeon respawns, notices); the rest stays in Almanac, and dry runs, check timing, the chest exclude list and fallback/import entries are marked as normally edited in the config file. Config keys, values and defaults are unchanged.

## 0.1.9 - batch 4E gameplay fixes (2026-10-19)

- Settings audit: chest settings apply live without a reload.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.8 - current-build preview refresh

Release stamps and dependency floors for the tested current UI/settings work. This is an intermediate build; expanded 4D and queued 4E are not completed. Exact checks and known limits ship in installer build evidence.

## 0.1.7 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.6 (2026-10-15)

- First release on Thunderstore.
