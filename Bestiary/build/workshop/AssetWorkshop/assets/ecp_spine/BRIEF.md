# Brief: `ecp_spine`

## 1. What it is and where it lives

- Name: `ecp_spine` (the item would be `ECP_Spine`).
- What it is: a length of a skeleton's spine, eight vertebrae still joined, lying on its belly with the spikes up: the
  bone the skeleton arsenal drops and its weapons are made from. It replaces the single vertebra (`ecp_vertebra`),
  which the user asked for on 2026-09-29: "I kinda actually want a spine item instead, it makes more sense".
- Mod: Elite Creatures Pack (skeleton arsenal, `Arsenal/`), once the user approves it.
- Biome: Black Forest and Meadows crypts and burial chambers, wherever the arsenal skeletons walk.
- Tier: 1 (bone, Black Forest).
- Who uses it: dropped as loot by the arsenal skeletons (1 in 10), carried, used in recipes.

## 2. Category

- Category key: `item.material` (family bone).
- Label and sample count: crafting material, 253 (bone family 17).
- Triangles: 12 / 96 / 144 / 300 / 14,222 (bone family median 129; BoneFragments 1,086, UndeadBjornRibcage 1,720,
  AsksvinCarrionRibcage 2,204: the game's bone heaps and skeletons run high).
- Texture px: 32 / 64 / 128 / 128 / 1,024 (bone family 256).
- Texel density: 19.6 / 35.8 / 49.4 / 70.3 / 1,477 px/m (BoneFragments 57).
- Longest side: 0.10 / 0.59 / 0.81 / 1.55 / 4.09 m (bone family median 0.84).

## 3. Game references

| Prefab | What to take from it |
| --- | --- |
| `GameElements/Items/materials/BoneFragments.prefab` | the item it is built on in the mod (its `attach`, its sounds); size 0.61 m, paint of small bone |
| `GameElements/Items/misc/WitheredBone.prefab` | a single big bone as an item: 1.98 m, 82 triangles, chunky |
| `GameElements/Items/materials/AsksvinCarrionRibcage.prefab` | a piece of skeleton as a material: about 1 m, 2,204 triangles |
| `GameElements/Items/materials/Wood.prefab`, `Stone.prefab` | the category's own references for size on the ground |

## 4. Silhouette

A row of big knobbly vertebrae in a gentle S, a crest of spikes along the top and wings out to the sides: reads as
"a skeleton's backbone" at 10 m.

## 5. Parts

- Origin and orientation: on the ground under the middle, Z up; the spine runs along X, the lumbar (tail) end at -X,
  the thoracic (neck) end at +X; spikes up (+Z), wings along Y, the bodies' fronts on the ground. Built like
  `ecp_vertebra` (the mod hangs it under BoneFragments' `attach`).
- Dropped item: 0.8 m long, about 0.3 m wide and high: about 3 times a real spine, the game's way with materials.

| Part | Size (m) | Share | Family | Region | Notes |
| --- | --- | --- | --- | --- | --- |
| 4 lumbar vertebrae | bodies 0.16 across, hatchet spikes, long wings | half the length | bone | bone | the arsenal's own vertebra design (`grave_vertebra`, low) |
| 4 thoracic vertebrae | bodies 0.13 across, long spikes sloping back, shorter wings | half | bone | bone | tapering to the neck end |
| 7 discs | 0.02 thick | gaps | bone, dark | secondary | dried cartilage: the dark gap the game paints between bones |

## 6. Budget

- Triangles: about 1,000 (the game's own bone heaps are 1,086 to 2,204).
- `TEXTURE_SIZE` 128; about 57 px/m (BoneFragments).
- `AO_STRENGTH` 0.2, `NORMAL_FROM_ALBEDO` 5.4 (bone's k).

## 7. Materials

| Family | Parts | Recipe | Palette | Notes |
| --- | --- | --- | --- | --- |
| bone | vertebrae | `paint.make("bone.bone", axis="X", density=57)` | the recipe's tones (Skeleton_d, SpineSnap_d, Boneshield_d, bonemass) | cream to ochre, stains |
| bone, dark | discs | `paint.make("bone.bone", tones=dark browns, region="secondary")` | #2e2014 / #4a3420 / #62472c | near-black gaps between bones |

Dressed in the mod on the skeleton's own material (`ArsenalLook.Dress`, `Custom/Creature`), as the vertebra is.

## 8. Paint regions

| Region | Parts | Variant |
| --- | --- | --- |
| bone | vertebrae | none planned |
| secondary | discs | none planned |

## 9. Rig and animation

None: a static item.

## 10. Effects

The game's own on BoneFragments (the dropped item's sparkle), kept by copying the prefab.

## 11. Sounds

BoneFragments' own (pick-up, drop), kept by copying the prefab.

## 12. Into the mod

Done 2026-09-29, for Elite Creatures Pack 0.5.0:

- Bundle `ecp_skel_arsenal` (`assets/ecp_skel_arsenal/build.ps1`: `ecp_vertebra` replaced by `ecp_spine` in its asset
  list, `icons.py` rendering the spine), copied to `EliteCreaturesPack/EliteCreaturesPack/assets/bundles`.
- Game prefab: `BoneFragments` (as the vertebra), model under its `attach`, box collider round it.
- Icon `ecp_spine_icon` (256 px like the arsenal's other icons).
- Mod code: `ECP_Vertebra` becomes `ECP_Spine` (item, word `item_ecp_spine`, drop 1 in 10, recipes, the greataxe's
  `Vertebrae` setting), README, CHANGELOG.
- Multiplayer: an ordinary item prefab registered on every peer, as the vertebra is.

## 13. Checks

- [x] `.\build.ps1 -Asset ecp_spine -Lineup` builds; `out/preview.png` looked at. 0.88 x 0.39 x 0.37 m, 1,516
      triangles, 128 px at 57.9 px/m (BoneFragments 57).
- [x] Style check: 16 of 17 PASS. The discs' blotch size is LOW (0.07 m against bone's 0.07 minimum): each disc is a
      2 cm ring, smaller than any blotch; they are meant as the near-black gaps between bones.
- [x] Lineup (`blender/lineup.py --refs AsksvinCarrionNeck,BoneFragments,WitheredBone,AsksvinCarrionRibcage,Wood,Stone`):
      the style check's nearest game item is AsksvinCarrionNeck, the game's own neck (eight fat drums, 0.97 m, 1,664
      triangles); the spine is the same size and texel look. First pass was spikier than it (thin wings like sticks):
      wings made shorter and half again as thick, thoracic spikes broader, all 20 % bigger.
- [x] Icon (`icon.py`, `out/ecp_spine_icon.png`): on the diagonal, fills 94 % of the square, luma 105 (game 80 to 120).
- [x] The user's verdict on the look: approved 2026-09-29 ("will you replace the vertabrae item with that spine?"),
      a visual and name change only; in Elite Creatures Pack 0.5.0 (bundle `ecp_skel_arsenal`, `ECP_Spine`).
- [ ] In game through DevBridge once it is in the mod.
