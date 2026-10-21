# Changelog

## 0.1.15 (2026-10-21)

- Requires Crucible 0.3.26 / Crucible.UI 0.3.19.
- Dedicated servers skip original item models, icons and materials while retaining all item and projectile registrations. Art failures on graphical clients warn without stopping prefab setup.
- A crypt chest claimed as a mimic keeps trying until this machine owns it and the mimic prefab exists. If that swap is given up, the chest is filled with its normal loot instead of staying empty.
- Taking a bone ballista checks that the sender is the player who asked and that they are at the ballista. A grant is ignored on a client that did not ask, and only the holder can let go.
- A greataxe spin no longer changes the attack every greataxe uses. Each item keeps its own attack. Damage, reload and the other live numbers still follow the prefab.
- A leaving kraken no longer writes the boss count from the owner's machine. Only the server lowers that count, from the server's own number.
- Mimic run speed follows a settings change on the prefab and on mimics already in the world.
- The compendium no longer builds a new list every frame after the book has been opened.

## 0.1.14 (2026-10-20)

- Requires Crucible 0.3.25 / Crucible.UI 0.3.18.
- License text: LICENSE_SCOPE.md no longer lists the Paws of the Bear triple strike, removed on 2026-10-15, among the adapted EliteCreaturesPack parts.

## 0.1.13 (2026-10-19)

- Requires Crucible 0.3.22 / Crucible.UI 0.3.15.
- Item settings (Bone Crossbow, bolts, Executioner Greataxe and other Bestiary items) now reach items already made: loaded and crafted items share their prefab's data instead of keeping a copy from when they were made.

### Simpler in-game settings

- Simpler in-game settings: ships settings-tiers.json, so the MeadWorks menu shows Bestiary's master switch and the seven creature/ballista switches; tuning stays in Almanac, and workshop, recipe, chest and biome lists are marked as normally edited in the config file. Config keys, values and defaults are unchanged.

## 0.1.12 (2026-10-18)

- Explain setting availability through the shared dependency metadata while preserving saved values and existing gameplay rules.
- Require Crucible 0.3.19 / Crucible.UI 0.3.12. Expanded 4D qualification is pending.

## 0.1.11 local art candidate (2026-10-17)

- Original weapons, shield, ammunition and loot models with matching icons; corrected crossbow and shield attachment frames.
- Native visual and fit qualification is pending; this entry does not record a publication.

## 0.1.10 (2026-10-15)

- First release on Thunderstore.
