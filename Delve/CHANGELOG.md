# Changelog

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
