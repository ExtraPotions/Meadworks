# Skeleton arsenal

The skeleton arsenal is every bone weapon of Elite Creatures Pack (the user, 2026-09-30): the dagger, sword, axe,
mace, spear, atgeir, the player's bow and the skeletons' bow, the arrow and the spine (this folder's shared code and
the `ecp_skel_*` and `ecp_spine` folders, bundle `ecp_skel_arsenal`); the Bone Crossbow and its blunted bolt
(`ecp_xbow_crossbow`, `ecp_xbow_bolt`, bundle `ecp_crossbowman`, built by `assets/ecp_crossbowman/build.ps1`); and
the Executioner's Greataxe (`ecp_bone_greataxe`, bundle `ecp_headsman`, built by `assets/ecp_headsman/build.ps1`).
The crossbow and the greataxe share their bundles with the creatures that carry them. This folder's `build.ps1`
builds all three bundles; `-SkipBlender -SkipBake -Install` rebuilds them from the models already built and installs
them into the mod.

The rest of this file covers the eight weapon assets built here: dagger, mace, spear, atgeir, sword, axe, bow and arrow.
All blades, heads, shafts and bow limbs are bone, with bone grips and coarse bone collars. The bowstrings and arrow feathers remain functional non-bone parts.
Every weapon incorporates anatomical vertebra geometry: dagger guard, sword grip,
mace head, axe and spear collars, atgeir spine, bow arrow shelf, and arrow bead.
Models are in the sibling `ecp_skel_<weapon>` folders, with editable Blender files,
FBX, albedo/normal textures and manifests in each `out` directory.


## Low-poly remake (2026-09-30)

The active arsenal now uses the same coarse, faceted bone treatment as the approved
Executioner's Greataxe. `low_vertebra.py`, `low_shapes.py`, `low_bones.py` and
`low_paint.py` rebuild the parts directly; this is not an automatic decimation pass.
Large guards retain their open canals, while small shaft vertebrae use solid
crests and wings. Hand positions and runtime string/socket points are preserved.
Textures are 32?128 px, point filtered. Sources and outputs remain in AssetWorkshop.

The current coloured model review is `out/remake/bone_arsenal_review.blend`;
open it with `--python open_remake.py`. It includes the greataxe, all eight
arsenal weapons, crossbow and blunt bolt, with each weapon scaled to fit its panel.
Select a weapon in the Outliner and press Numpad . to frame it for inspection.
The triangle labels count exported models; runtime bowstrings appear for preview only.
`remake_review.py` rebuilds the review and icon references. `check_remake.py`
checks geometry, dimensions in manifests, texture budgets and attachment contracts.
`out/remake/validation.json` and `report.md` record the result.
The rebuilt set totals 11,446 triangles (48,338 before), including both bow holds.
Inventory icons are transparent 128 px PNGs beside each model.py; generated masters
and 64 px copies are in out. `out/remake/icons-review.png` shows the full set, and
`remake-icon-prompts.json` records the built-in image_gen prompts.

The animated showcase below predates this remake; rebuild its Unity caches before
using it to judge the new geometry. The remade bundles (arsenal, crossbow set, Executioner) were
installed into Elite Creatures Pack on 2026-09-30 at the user's request; unreleased, not yet seen in game.

## Watch in Blender

```powershell
& "$env:USERPROFILE/tools/blender/blender.exe" ./out/blender/skeleton_arsenal.blend --python ./blender_play.py
```

Run the command from this folder.
Space pauses/resumes. The 50-second timeline tracks along the lineup, then cuts to
Dagger, Sword, Axe, Mace, Spear, Atgeir and Bow close-ups. Each attacks repeatedly.
The final shot shows the spine the skeletons drop. Named cameras are in the Outliner.

The characters are the original game's Skeleton visual and skin, baked in Unity
through its humanoid Animator. Sword, mace and bow use the creature's game clips;
dagger, axe, spear and atgeir use the game's corresponding player weapon clips on
the Skeleton avatar. The bow has an animated string, nocked arrow and arrow flight.
These are vertex-cache animations in Blender; editable skeleton bone actions are
not included. Rebuild their motion through `unity/Assets/Editor/SkelArsenal`.

Keep the seven `out/blender/ecp_skel_*` cache folders beside the `.blend` when moving
the scene. Textures are packed. Original game content remains in local reference
and preview output; the weapon bundles contain the custom assets.

## Review and validation

- `out/arsenal_sheet.png`: all custom models and detail views.
- `out/attack_review.png`: three frames from every showcase shot.
- `out/reference_audit/inventory.json`: 284 files from the local exported Skeleton folder.
- `out/reference_audit/review.json`: original prefab render results, with source paths.
- `out/reference_audit/creatures.png`: 13 visible creature variants in manifest order.
- `out/reference_audit/weapons.png`: 13 original weapon variants in manifest order.
- `out/validation.json`: eight export checks, seven skeleton/weapon motion checks,
  finite cache coordinates, matching vertex/frame counts, texture/cache dependencies,
  and a bow release frame inside the attack loop.

`Skeleton_aspect.prefab` is inventoried but gives no visible mesh through the reference
importer. Particle effects and game shaders are not reproduced exactly by Blender.
The audit covers the local reference export, rather than asserting coverage of a
different installed game version.

The weapon models, Unity animation bake and original showcase already existed in
the workspace when reviewed. This pass audited the references, rendered attack
checks, corrected reversed detail-sheet framing, packed the scene textures, made
cache paths relative, and added playback confirmation and dependency validation.

`build.ps1 -Open` rebuilds the assets, bundles, animation bake and scene and opens
Blender. `reference_audit.py`, `present.py` and `validate.py` run through Blender's
`--background --python` option; validation takes the showcase `.blend` as input.
