# Larder feature-icon model source

Original designs by expDARE, from approved sheet `07-larder-icons.png`. Run `python generate.py` to rebuild deterministic `models.json` (schema 1, meters, Y up). This uses the adjacent Shepherd `geometry.py` and interchange exporter.

The twelve icon scenes cover Cooking, Food, Mead, Fermentation, Cook Time, Burn Warning, Health Recovery, Stamina Recovery, Eitr Recovery, Rested, Extra Food and Saved Ingredients. They are feature symbols and representative existing-food styling. There are no new item identities, inventory replacements, recipes, status effects or skill identities. Each rendered icon must use its exact authored mesh scene so the source and icon cannot diverge.

Cooking's established runtime seam now consumes `OriginalItemArt.Icon("larder_cooking")`. The retained adopted `Larder.skill_cooking.png` resource and provenance are preserved. All twelve symbols are available in the asset library. The existing `SkillPageLine` baseline has no sprite field, so the other eleven symbols are not injected into the text-only page. Do not replace native food/mead inventory icons globally with generic feature symbols.

The meshes have no colliders and no runtime prefab IDs. They are icon-authoring geometry, not proposed world pieces. Use the native wood, iron, stone, wool and food texture treatment, warm upper-left icon light, transparent backgrounds and subdued color. Spirals and embers carry restrained emission. `geometry-preview.png` is a flat-shaded CPU debug preview with painter-sort artifacts, not a final icon render. Final rendering, compilation, small-size readability and native visual qualification remain for the root-owned render/build/game lane.

Procedural source: GPL-3.0-or-later. Generated original models and corresponding rendered art: CC-BY-NC-SA-4.0, expDARE. No game mesh or texture is included.
