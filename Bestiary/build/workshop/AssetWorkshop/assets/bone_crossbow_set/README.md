# Gravebranch — bone crossbow and matching quarrel

Original Valheim-style weapon art, built in Blender and imported into Unity 6000.0.75f1.
The wide laminated rib limbs, carved long-bone stock, sinew lashings and dark leather follow
the installed game's bone equipment. The bolt is a sharpened bone shaft with a chipped,
barbed bone head and three dark raven flights. Muted ivory, ochre roots, flat facets and
point-filtered texture atlases keep it readable at Valheim's normal gameplay distance.

## Open the result

- `out/presentation.png` — labelled model sheet with the matching bolt, top and underside views.
- `out/hero.png` — large view rendered from the actual exported meshes and baked textures.
- `out/Gravebranch_editable.blend` — individual named parts, editable materials, string shape
  keys, attachment markers and a 90-frame firing/reload demonstration. Press Play at 30 fps.
- `out/Gravebranch_review.blend` — packed baked models with the bolt seated and a second bolt alongside.
- `out/fire_reload.gif` — the Blender firing demonstration.
- `out/Gravebranch.unitypackage` — importable prefabs, original models, textures, icons and review controller.
- `../../unity/Assets/Preview/BoneCrossbowReview.unity` — local Unity review scene; Play animates the shot/reload.
- `out/bundles/ecp_bone_crossbow_set.windows` and `.linux` — platform-specific asset bundles.

All local reference exports are outside the distributable bundle/package. The original meshes
and textures can be used without bundling Valheim's files. The existing skeleton crossbowman
and its weapon assets have not been replaced.

## Reference survey

`reference_audit.py` indexes the local Valheim reference export and follows recipe references
to bone fragments, withered bones, charred bones, hard antler, wolf fangs and bonemaw teeth.
This finds bone-consuming armor/upgrades as well as visibly bone-built equipment. The broad
inventory includes 65 matching prefabs (including related/non-bone name matches), 59 item
icons, and recipe provenance under `out/reference/`. It is a survey of this local export,
not a claim about every Valheim release or whether unused prefabs are obtainable in play.

Actual mesh and material references were rendered for bone fragments, withered bone,
bone tower shield, wishbone, vanilla bone bolt, hard antler, wolf fang, charred bone,
bonemaw tooth, asksvin skull, fang spear, arbalest, Spine snap, Dead raiser,
Stagbreaker and the bone turret bolt. Reference preview meshes are normalized individually
for visual inspection; those sheets do not represent relative in-game sizes. They show
the first mesh/material of multi-part items, with a simplified shader. The icon sheet
also captures the complete item silhouettes, including the bone flute.

Primary design choices came from the bone shield's ribs and bindings, bone fragments'
ivory facets, the arbalest's functional layout, and the existing bone bolt's carved shaft.
Our new shapes and painted texture pattern are original.

## Assets and scale

| Asset | Triangles | Atlas | Dimensions (Unity X/Y/Z, metres) |
| --- | ---: | ---: | --- |
| `ecp_bone_crossbow` | 5,308 | 512 x 512 | 1.090 / 0.220 / 1.140 |
| `ecp_bone_crossbow_unloaded` | 5,308 | 512 x 512 | 1.090 / 0.220 / 1.140 |
| `ecp_bone_quarrel` | 1,020 | 256 x 256 | 0.063 / 0.055 / 0.510 |

Each sibling asset directory has an `out/` containing FBX, packed blend, albedo, transparent
128px inventory icon and JSON manifest. Each static visual is one mesh and one material.
The loaded crossbow mesh includes its drawn string; it does not bake the bolt into the mesh.

`ecp_bone_crossbow_assembly.prefab` combines `Loaded`, `Unloaded`, and `Bolt` children,
with only `Loaded` and `Bolt` visible initially. Switch the two body states and hide
the seated bolt when a gameplay projectile is spawned. Both body states currently share
the same limb shape; only the string changes. The Blender source interpolates the string;
the Unity review switches between the supplied static states.

The assembled weapon and `ecp_bone_quarrel_projectile` face Unity **+Z**, up **+Y**.
Blender source faces **-Y**, up **+Z**. The bolt's local origin is near its nock.

| Marker | Unity local position |
| --- | --- |
| Grip | (0, -0.035, -0.090) |
| SupportHand | (0, -0.055, 0.290) |
| BoltSeat | (0, 0.058, 0.075) |
| Muzzle / loaded tip | (0, 0.058, 0.565) |

These are model-relative fitting guides, not a verified player hand attachment transform.
Assembly has a simple box collider; remove/disable it when attaching as a held visual.
The projectile prefab is a visual and collider, ready to replace the visual on a game
projectile clone. Inherited gameplay colliders should govern flight collision.

## Integration boundary

This is an art asset kit, not an installed gameplay mod. The firing scenes demonstrate
appearance and motion; they do not implement damage, hit detection, ammunition consumption,
network spawning, a crafting recipe or registration in ObjectDB/ZNetScene. A mod should
clone the game's crossbow and projectile behavior, attach these visuals, drive the loaded
state, register a separate ammunition item if desired, and fit hands to the intended user.
Replace the Standard carrier shader with the game's item shader while retaining these
original albedo textures. No in-game firing test has been performed.

## Rebuild and validation

From the workspace:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File AssetWorkshop/assets/bone_crossbow_set/build.ps1 -References -Animation
```

Use `-SkipBlender` to repack existing output. Blender/Pillow handle the review output;
Unity must be closed before its batch build starts.

`validate.py` checks saved meshes for finite coordinates, closed manifold parts, degenerate
faces, outward aggregate volume, UVs, packed original textures, triangle budgets, and the
saved shot/reload states. Unity checks imported dimensions, forward bolt placement, initial
visibility, forward shot motion, reference dependency exclusion, and both platform builds.
Results are in `out/blender_validation.txt`, `out/unity_validation.txt` and `out/unity.log`.
