# Rebuild the original item art

Authored by expDARE. This source kit contains the editable model descriptions, their generators, original material atlas, Unity builder and runtime art loader. Code uses GPL-3.0-or-later; original visual assets use CC BY-NC-SA 4.0. Keep the product's license files and existing third-party notices with corresponding source.

## Rebuild bundles and icons from the supplied models

Use PowerShell 7 on Windows, the licensed Unity **6000.0.75f1** editor with Windows and Linux Standalone build support, and an existing compatible Unity project that already compiles. The builder uses Unity's built-in Standard shader. Close that project in Unity and coordinate the machine's build/game lane first. The helper preserves running Valheim sessions by refusing to start while they exist. It never creates a Unity project.

From this `source` directory, run:

```powershell
.\Rebuild-OriginalArt.ps1 -Product Mantle `
  -UnityProject 'D:\ExistingUnityProject' `
  -UnityEditor 'D:\Unity\6000.0.75f1\Editor\Unity.exe'
```

Replace `Mantle` with this kit's product. An optional `-OutputDirectory` must name a new or empty directory. By default the helper creates a unique `rebuilt/<product>-<timestamp>` directory beside itself. It temporarily places the supplied `BuildCustomItems.cs` at `Assets/Editor/CustomItems/BuildCustomItems.cs` in the existing project, runs `MeadWorks.CustomItems.BuildCustomItems.Build`, then restores the previous builder. Generated Unity assets remain in `Assets/CustomItemsGenerated/<product>`; the export directory retains its log, receipt, both bundles and PNGs. A rebuild is bounded to ten minutes and stops only its verified owned process on failure or timeout.

The builder reads only these supplied art inputs:

| Input | Purpose |
|---|---|
| `<product>/models.json` | Meter-scale geometry, part materials, UVs, camera poses, anchors and collider descriptions |
| `textures/materials-atlas.png` | Original grayscale grain atlas, sampled for matching material names and tinted with their palette; loaded and hash-checked |
| `BuildCustomItems.cs` | Model/material construction, exact-model icon rendering and Windows/Linux bundle export |
| `textures/provenance.json` | Original atlas prompt, recorded source and SHA256; retained provenance rather than a build dependency |

The script checks the resulting receipt against the exact model, builder and atlas hashes. It exports `mw_<product>_items.windows`, `mw_<product>_items.linux`, `<asset-id>_icon.png`, source records and `build-result.json`. Rendering requires graphics; do not add `-nographics`. Rebuilt bytes can vary with Unity or graphics configuration even when the editable model inputs match. A rebuilt artifact needs its own verification.

To integrate an intentional rebuild, copy its matching bundles into the product repository's `assets/original-items/` and icons into `assets/original-items/icons/`, preserve `catalog.json`, and refresh that directory's hash inventory. Rebuild the product plugin through its existing project and packaging workflow. This helper does not modify a plugin, install profile, save, world or release package.

## Editable generation and checks

The product subdirectory contains all its `.py` generators/helpers and `.json` model/check records. Product README files are included where present. Larder's kit also includes the sibling `shepherd/` directory because Larder's generator imports its mesh and export helpers. This dependency does not add Shepherd gameplay to Larder.

Python is optional when rebuilding directly from the supplied `models.json`. Regenerating models requires Python 3 and the included helpers; authoring preview tools can additionally require Pillow and NumPy. Some source checks read the surrounding product repository's catalogs or runtime source and therefore need the complete corresponding repository, not only this art subdirectory. The model JSON and Unity builder remain sufficient for the bundle/icon rebuild above.

`source-kit.json` records this kit's product and paired product version, exact input hashes and included authoring directories. The parent `assets/original-items/SHA256.json` records the copied source and artifact files. `OriginalItemArt.cs` is the corresponding runtime loader source; each product's actual integration, resource names and material catalog live in its repository.

## External runtime dependencies

Unity bundle creation needs Unity Editor APIs, its built-in Standard shader and platform modules. It does not require Valheim assemblies, Newtonsoft.Json or game shader exports. The reusable project must be free of unrelated compilation errors and conflicting copies of the builder class.

Running the finished plugin requires the user's installed Valheim and the product's declared BepInEx, Jotunn and Crucible dependencies. The runtime loader uses the game's Newtonsoft.Json assembly for catalog decoding, Unity runtime APIs for loading bundles, and native prefab materials/shaders borrowed from that installed game. Product builds reference those dependencies through their existing `.csproj` and `RuntimeReferences.props`; follow the repository's build instructions and matching dependency versions.

No Valheim DLLs, extracted game textures or game shaders are included in this source kit. The original atlas, model palettes and procedural grain supply distributed texture pixels; native game materials are resolved only at runtime. Native shader appearance, equipped poses, pickup/collision behavior and feeding-state behavior must be checked in the game after rebuilding.
