# Executioner's Greataxe handle remake

## 1. Identity
`ecp_bone_greataxe`, the bone Executioner's Greataxe used by the Headsman and
player in Elite Creatures Pack. Existing undead/bone theme, Black Forest/Swamp
visual palette; gameplay tier and distribution stay as configured.

## 2. Category
`weapon.axe_2h`: six game designs, 188–3,023 triangles (median 1,080),
64–128 px textures, 36–82 px/m (median 66), 1.61–1.82 m held length.

## 3. References
Battleaxe for coarse shapes and handle thickness; BattleaxeCrystal for two-hand
proportions; SpineSnap for bone and hide; Skeleton for the bone palette.

## 4. Silhouette
Keep the approved bearded head and exact S-curved centreline; replace the dense
mechanical-looking chain with chunky, irregular bone bodies and broad crests.

## 5. Parts and placement
Grounded origin, Z up, blade +X. Keep the existing 1.5 m workshop model; the
existing Unity attach scales it to 1.7 m and places the fists. Dropped size,
physics and attachment are unchanged. Building-piece fields do not apply.
Twelve bone bodies along the 1.1 m haft, approximately 5–8 cm thick; a continuous
recessed bone core; rear crests and paired side processes, shortened at grip seats
at heights .08, .34 and .73 m; angular heel and flared head seat.

## 6. Budget
New handle target under 1,500 triangles; approved head exempt from simplification.
Retain 256 px atlas to preserve the existing head paint through rebaking, an
explicit exception to the category's 128 px maximum. Aim for coarse 65 px/m
paint features on the new haft. AO 0.2; no normal map, preserving the existing
albedo-only integration and the approved head shading.

## 7. Materials
Workshop bone recipe with broad tan/cream blotches, no fine grain or extra edge
brightening; brown recessed joints. Bone midtone linear RGB (.30,.235,.15).
All parts are bone; no leather, bindings or other materials.
Approved head mesh and its original UV-driven material are retained.
Runtime material remains the existing Custom/Creature dressing.

## 8. Regions
Bone on new parts; retained head remains base. No variants requested.

## 9. Rig
Rigid weapon, no new rig or clips. Preserve spine_point exactly for existing
Headsman and player grip calculations. Skeleton idle preview uses existing IK.

## 10. Effects
None authored: model-only revision; existing gameplay effects unchanged.

## 11. Sounds
None authored: model-only revision; existing gameplay sounds unchanged.

## 12. Integration
Build editable packed Blender file, FBX, albedo, region mask and previews in
the workshop. Existing ecp_headsman bundle path can consume the same asset on
a later release. No mod installation, new recipe, collider, config, multiplayer
state or icon change is part of this model revision.

## 13. Checks
Render four views, compare against game references, run category style report,
validate total height and retained head geometry, and check the existing held
idle. Document numeric exceptions after the build. In-game review is pending.

Completed: inspected four-view preview, held Skeleton and game reference lineup.
2,916 triangles total, 1,284 in the new handle. Head vertex displacement is exactly
zero; overall height is 1.500 m. Held idle passes: 0.063 mm maximum hand IK error,
0.007 mm foot drift, effectively zero loop seam.

Style report: 9/12 judged lines pass, including all bone paint metrics.
Texture size (256 px) and texel density (112.5 px/m) are high because the original
head texture is preserved through a shared rebake; new handle paint uses broad
features despite the denser atlas. Workshop length (1.5 m) is below the game's
held range because the existing attach scales this source to 1.7 m. These three
exceptions preserve the approved head and existing integration contract.
Bone region mask is generated; no recolour variants requested.
No in-game installation or dedicated-server testing was performed for this model revision.

