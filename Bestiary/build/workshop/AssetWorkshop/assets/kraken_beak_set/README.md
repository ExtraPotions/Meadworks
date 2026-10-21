# Kraken beak and beak shield

Two original standalone assets. The beak uses dark aubergine horn with worn amber cutting edges, subtle crown growth ridges, and a smaller lower jaw. The standalone beak and mounted beak share identical geometry and scale. The shield has a gently bowed body of individual pale finewood planks, silver edging, rivets, diagonal mounts, small knot ornaments, inset border details, curved rear braces, and one finewood grip with silver ferrules behind the middle of the board. The edges sweep back by about 8cm relative to the center. The beak itself is not bent with the shield.

Intended shield recipe: **10 FineWood, 5 Silver, 1 kraken beak**. This is an art specification for later gameplay integration; the custom beak item's runtime prefab name has not been registered.

Vanilla serpent-scale shield artwork was inspected as a palette/texture reference. No game mesh or texture was copied into these assets.

## Outputs

- `../ecp_kraken_beak/out/`: packed Blender model, FBX, 256px albedo, collider manifest, 128px transparent icon, preview. 1,224 triangles; approximately 43cm wide, 41cm high, 54cm deep.
- `../ecp_kraken_beak_shield/out/`: equivalent shield files with 512px albedo and a rear preview. 4,684 triangles; approximately 76cm wide, 105cm high, 67cm deep including projecting beak and grips.
- `out/Kraken_Beak_Review.blend`: both models at actual relative scale with packed textures and material-preview viewport.
- `out/kraken_beak_pair.png`: comparison render.

Model coordinates use metres, Blender Z up and front -Y. Box colliders are supplied for import/pickup use. Shield origin is near its bottom; hand attachment/pivot alignment must be configured when integrating an equippable item.

No Unity bundle, gameplay registration, active crafting recipe, drop changes, or mod installation is included.

## Rebuild

Run Blender in background mode with `--factory-startup --python-exit-code 1 --python AssetWorkshop/assets/kraken_beak_set/present.py` from the repository root. This regenerates the output files for these two assets. Geometry and palette are in `design.py`.
