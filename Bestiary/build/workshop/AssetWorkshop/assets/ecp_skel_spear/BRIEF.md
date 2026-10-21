# Low-poly bone remake: ecp_skel_spear

## 1. Identity and setting
Existing Elite Creatures Pack bone spear; preserve gameplay tier, distribution and undead theme.
Black Forest/Swamp bone palette, shared with the approved Executioner's Greataxe.

## 2. Category
weapon.spear. See codex/models/weapons.md and codex/data/items.json for the measured game range.

## 3. References
SpearBronze,Skeleton: base-game silhouette, texture coarseness and bone colours; Executioner's Greataxe for the approved new treatment.

## 4. Silhouette
Preserve the current weapon type, blade outline and anatomical bone concept.
Replace tiny repeated anatomy with broad faceted vertebrae, crests and side processes.

## 5. Parts and placement
Keep model.py's existing length, origin, hold transforms and attachment constants.
Main shafts, heads, grips and blades remain bone; the arrow keeps its flight feathers.
Bows/crossbows keep the exact string endpoints. Bone collars replace fine cord wraps.
No building-piece or terrain placement fields apply.

## 6. Budget
Target at most 1800 triangles, 64px atlas, coarse paint features at 65 px/m.
AO 0.2. Albedo-only material, no new normal-map detail; shading comes from the facets.

## 7. Materials
low_paint bone recipe: warm ivory/tan, linear midtone (.30,.235,.15), broad soft blotches,
dark bone joints and pale ground blade edges. No metal or wood. Existing Custom/Creature dressing retained.

## 8. Regions
Bone region for the new mesh; arrow feathers are base. No recolour variants requested.

## 9. Rig and animation
Rigid item. Preserve game hold frames, bow string points and crossbow sockets.
No new attack clips, rig, animator events or root motion.

## 10. Effects
No changes; existing gameplay effects remain outside this model revision.

## 11. Sounds
No new sounds; existing gameplay audio remains unchanged.

## 12. Integration
Workshop-only rebuild of Blender, FBX, atlas and inventory icon. Existing bundle builders
consume the same names on a later release. No install or mod code changes.
Physics, recipe, config and multiplayer state remain unchanged.

## 13. Validation
Check final mesh triangles, dimensions and finite geometry; compare attachment constants
with the archived source. Review coloured preview and game reference lineup. Run style
report and record exceptions in out/remake/report.md. Icons must be transparent and
readable at inventory scale. In-game multiplayer validation is not part of this asset review.

Result: 1,354 triangles, 64 px atlas; mesh and attachment checks pass.
Reference lineup and coloured review inspected. Remaining style exceptions are
listed in ../ecp_skel_arsenal/out/remake/report.md. Not installed or tested in game.
