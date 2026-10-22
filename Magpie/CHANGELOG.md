# Changelog

## 0.1.13 (2026-10-21)

- Thunderstore republish of the release 8 build; no code changes.

## 0.1.12 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- A merge that removes a source stack saves that fact on the target before the source is deleted. Reloading does not take those items back, and the removal is not applied twice. If the source is lost before that removal, a target that was already partly picked up is not reduced below its count from before the merge.
- A loot extra that only partly fits drops the leftover count once. Units that already merged into a stack are not spawned again.
- German, Russian, Chinese and Korean pickup text uses the game's words for pickup, floating loot, graves, chests and crafting stations.
- A ground merge that loses the source stack before that stack is shrunk or removed takes the extra items back off the target. The picked-up stack is not duplicated. A merge that already finished keeps its result.
- Float settings no longer build a comparison string every frame. What floats does not change.

## 0.1.11 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.

### Batch 4D settings text

- Float Enabled and Float / Excluded Items now say that changes also apply to items already in the world.

### Simpler in-game settings

- The MeadWorks menu shows Magpie's Essential settings first.
- Every setting is still available in Almanac; performance internals and item lists stay in the config file.
- Config keys and saved values are unchanged.

## 0.1.10 - batch 4E gameplay fixes (2026-10-19)

- Settings audit: floating item settings apply live.
- Requires Crucible 0.3.21 / Crucible.UI 0.3.14.

## 0.1.9 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.8 (2026-10-15)

- First release on Thunderstore.
