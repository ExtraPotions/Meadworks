# Changelog

## 0.1.18 (2026-10-22)

- Version 0.1.18.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20.
- Each Slag Charred side plate sits another 0.06 m outward (0.79 m from the chest) so the idle forearm clears it. The front plate is unchanged, and the plates still do not overlap from the front.
- The Fuling Spearman copy equips the game's spear (GoblinSpear, then SpearFlint, then SpearWood) and does not wear a stand-in spear. The shared Fuling prefab and its shield roll are unchanged.

## 0.1.17 (2026-10-21)

- Version 0.1.17.
- Requires Crucible 0.3.28 / Crucible.UI 0.3.20.
- Requires Wildblood 0.5.5.
- A Slag Charred plate takes the fire on a direct hit. Burn, freeze, and poison ticks do not melt a plate.
- Each side plate sits another 0.08 m outward (0.73 m from the chest) and 0.04 m further forward (0.12 m). The front plate is unchanged, and the plates still do not overlap from the front.
- The Fuling spear grip is 0.12 of the length from the butt, and the head tilts 22 degrees up instead of 25. In the idle swing that put the butt 0.05 m into the ground, the butt stays about 0.19 m up. The head still points forward and up.

## 0.1.16 (2026-10-21)

- Requires Crucible 0.3.27 / Crucible.UI 0.3.20.
- Requires Wildblood 0.5.5: the Marrow Lantern's Reveal uses its creature rules, which Wildblood 0.5.4 ignores.
- Four new creatures: Draugr Hook, Fuling Spearman, Slag Charred and Marrow Lantern spawn on the Draugr, Fuling, Charred warrior and Deep North skeleton. No new materials, recipes, boss bar or world defeat key.
- They wear a game hook, spear, buckler and lantern found by prefab name as stand-in props. If that prefab is missing, the prop is left off and the fight still runs. Dedicated servers register the creatures and run the fights without props, materials or textures.
- Draugr Hook, Fuling Spearman and Marrow Lantern hold their hook, spear and lantern at about 1.2 m, 2.2 m and 0.5 m on the right hand. The Fuling Spearman holds the spear like a javelin: the wide head points forward and up, and the grip is a quarter of the way from the butt. Slag Charred plates sit on the upper chest at about 0.45 m each, with the buckler dome facing out, and they do not cover each other from the front.
- One Ashlands region gets one Slag Charred. The server records the claim before the creature spawns, so two zones rolling together cannot both spawn. A full Delve regenerate clears that claim and a Rime Giant's mountain claim. Marrow Lanterns have no claim.
- Fire on a hit breaks a Slag Charred plate, a partial plate is kept, and a broken plate is no longer shown. The hook's cooldown is kept. The hook and the spear hit harder with stars, the same way the body's own attacks do.
- A hook pull is accepted only from the owner of a living Draugr Hook in range, and the drag stops at the Pull setting.
- Marrow Lantern Reveal is the distance at which that skeleton's own cloak drops when Wildblood gives it Cloaked. It does not reveal creatures around it. A Reveal of 0 is sent to Wildblood as cloaked_reveal 0.
- A Marrow Lantern rolled from a location with an enemy level override still starts at level 1. Wildblood can still replace that level.
- The lantern compendium no longer says a cloaked creature is revealed just because Wildblood is installed. French, Russian and Chinese use Valheim's Drake names.
- Elite Creatures Pack still makes the whole product step aside, including these creatures.

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
