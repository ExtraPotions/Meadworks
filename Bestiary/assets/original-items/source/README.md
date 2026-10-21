# Original custom item art

Authored by expDARE. Code and generators use GPL-3.0-or-later; original visual assets use CC BY-NC-SA 4.0. Existing third-party notices remain in the product repositories.

The editable meter-scale meshes are in each product's `models.json`, with Python generators beside them. The catalog contains 21 Mantle items, 21 Inscribe items, 17 Bestiary items, three Shepherd feeding pieces, and 12 Larder feature-icon scenes. Larder scenes do not introduce inventory items or recipes. Cooking uses the existing skill-icon field; the other scenes remain available in its asset library.

`textures/materials-atlas.png` supplies original wood, leather, fur, bone, metal, stone, parchment and cloth grain for matching material names. The exporter normalizes each tile's grayscale values and tints them with the material palette; other materials use procedural grain. Its prompt, source and hash are in `textures/provenance.json`. Exported models and icons use the same geometry and materials. Native shaders are borrowed at runtime from the player's installed game; no extracted game textures are distributed here.

## Rebuild

For a standalone copy under a product's `assets/original-items/source`, use `SOURCE-REBUILD.md` and `Rebuild-OriginalArt.ps1`. They use local art inputs and caller-supplied Unity paths. The commands below describe the staging workflow with a shared Unity project.

Use a Unity 6000.0.75f1 project with Windows and Linux support (`<unity-project>` below).

1. Run `python stage_assets.py --catalogs --editor` from this folder.
2. Launch the graphics-enabled Unity editor in batch mode with `-projectPath <unity-project> -executeMethod MeadWorks.CustomItems.BuildCustomItems.Build -customItemsSource <absolute product/models.json> -customItemsOut <absolute build/product> -logFile <absolute unique log>`. Omit `-nographics`; transparent icons require rendering.
3. After a successful build, run `python stage_assets.py --built <Product>`. The script checks source, builder and atlas hashes before staging both bundles, 256-pixel icons and corresponding editable source into that product's `assets/original-items`.
4. Build the affected plugin against the matching Crucible dependencies.

`preview_models.py` creates source diagnostics at 256, 64, 48 and 32 pixels. These software renders do not establish Unity shader behavior, equipped fit, physics, feeding, persistence or in-game visual acceptance.

`textures/materials-atlas.png`: Embedded metadata removed 2026-10-20 at the author's request; pixels unchanged.
