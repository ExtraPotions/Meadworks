# Shepherd model source

Original mesh designs by expDARE, from approved sheet `08-shepherd-troughs.png`. Run `python generate.py`, then `python check_models.py`. The deterministic interchange file is `models.json` (schema 1, meters, Y up, +Z animal-access front).

The feed barrel has individual staves, a low cut front, dark hoops and a partial lid leaning behind its open basin. The plank trough has joined boards, resin seams, square nail heads and low splayed feet. The stone trough uses separate irregular dressed blocks over two timber sleepers. Mesh parts marked `collidable` are the physical shell and floor. The aggregate `collider` value is only the broad size envelope; applying it as a solid collider would obstruct the basin.

All three expose `food_surface` and `attachment.foodSurface` bounds at the basin floor. Use the real stored edible item's native meshes for food display, constrained to those bounds, with no baked food mesh. Render build icons from the same `always` mesh parts. Empty and filled geometry is identical. Retain existing prefab IDs and the `grindstone_feeder` alias, recipes, storage behavior and saved contents.

The production hookup replaces native source renderers and source chest colliders before a placed instance awakens, preserves the native collision layer, and uses the shell colliders and matching build icon. `TroughFoodVisuals.Surface()` now uses the explicit minimum/maximum basin anchors, avoiding food floating above the rear barrel wall/lid. Original game shader behavior is adapted locally without redistributing game assets.

`SOURCE-CHECKS.json` covers mesh source geometry only. `geometry-preview.png` is a flat-shaded CPU debug preview with painter-sort artifacts, not a final icon render. Final icon rendering, compilation, placement, interaction, feeding and save/reload remain for the root-owned Unity/native lane.

Procedural source: GPL-3.0-or-later. Generated original models and corresponding rendered art: CC-BY-NC-SA-4.0, expDARE. No game mesh or texture is included.
