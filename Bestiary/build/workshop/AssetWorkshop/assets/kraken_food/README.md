# Kraken meat assets

Original standalone raw and cooked tentacle portions for Elite Creatures Pack. The raw palette follows the existing kraken; vanilla serpent meat textures were viewed as style references only. No vanilla meshes or textures are included.

- `../ecp_kraken_meat/out/`: raw model, FBX, 256px baked albedo, collider manifest, 128px transparent inventory icon, and 512px preview.
- `../ecp_kraken_meat_cooked/out/`: cooked equivalents, slightly shrunken with toasted suckers and continuously mottled browned skin.
- `out/Kraken_Meat_Review.blend`: both fully textured models together, textures packed, material preview configured.
- `out/kraken_meat_pair.png`: comparison render, raw left and cooked right.

Each visual contains 1,028 triangles and one baked material. Models use meters, Blender Z up, and an approximately ground-level pivot. The supplied box collider covers the portion for pickup use. FBX and the adjacent texture/manifest are AssetWorkshop inputs; no Unity bundle, item registration, recipe, or kraken drop changes are included.

Source geometry and palettes live in `food.py`. Rebuild both assets, icons, and review with Blender in background mode using `present.py`. This replaces the generated output files in the two food asset directories.
