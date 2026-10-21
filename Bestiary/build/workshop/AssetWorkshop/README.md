# AssetWorkshop

3D assets for the mods, made from scripts. Blender builds and textures a model from a Python file, renders a
preview sheet to check it by eye, and exports it. Unity packs one or more models into an asset bundle that a mod
embeds, and ValheimModLibs' `BundlePrefabs` brings it into the game. Creatures go further: a rig, animation clips, an
animator controller built against the game's creature code, previews staged in Blender and replayed live in Unity.
Nothing here ships on its own.

```
AssetWorkshop/
  build.ps1            Blender for each asset, then optionally Unity for the bundle
  rip-reference.ps1    exports the game's own assets as a local reference project (never committed)
  blender/
    run.py             Blender entry point for one asset
    workshop/          helpers: shapes, materials, bake, export, preview, pipeline
  assets/<name>/
    model.py           the asset: build() makes the meshes
    out/               build output (gitignored)
  assets/crypt_mimic/  the first creature (see "Creatures")
  assets/ecr_slinger/  a game creature with new gear, the Greydwarf Slinger (see its section)
  assets/ecr_rimegiant/ gear fitted to a game creature's body, the Rime Giant (see its section)
  assets/ecp_kraken/   a creature of our own animated in code, the Kraken (see its section)
  assets/ecp_crossbowman/ a game creature with new gear and clips authored on its own skeleton (see its section)
  assets/ecp_spine_greataxe/ a giant all-bone greataxe, shown in the game's Skeleton's hands (see its section)
  assets/ecp_battleaxe_bone/ a bone battleaxe sized and budgeted like the game's own (see its section)
  assets/workshop_gamerig_demo/ a new creature body on an exact copy of a game skeleton, the Mossback (see its section)
  assets/ecp_deathsquito_queen/ a flyer rigged whole-part on a game skeleton, her fight keyed in Blender (see its section)
  codex/               what the game's own assets measure: the rules every new asset is built to (see "The codex")
  vfx/                 particle effects: spec, game-style textures, build, preview (see "Effects")
  sfx/                 sounds: synthesis, recipes, comparison with the game's, bundle (see "Sounds")
  unity/               the bundle project (Unity 6000.0.75f1); Assets/Editor holds the batch builds,
                       Assets/Preview/Scripts the runtime preview (fight replay, HUD)
  out/bundles/         built bundles (gitignored)
```

## Tools

Installed on 2026-09-27 into `%USERPROFILE%\tools`, so nothing needed admin rights.

| Tool | Where | For |
| --- | --- | --- |
| Blender 5.2.1 LTS, portable | `tools\blender\blender.exe` | modelling, baking, previews; run headless |
| Unity 6000.0.75f1 + Linux build support | `tools\Unity\Editor\6000.0.75f1\Editor\Unity.exe` | building bundles (Windows and Linux); run headless |
| Unity Hub 3.21 (winget, MSIX) | Start menu | installs editors |
| AssetRipper 2.0.0 | `tools\AssetRipper\AssetRipper.GUI.Free.exe` | the reference export |

`build.ps1` finds Blender and Unity there; override with `-Blender`/`-Unity` or the `WORKSHOP_BLENDER`/`WORKSHOP_UNITY`
environment variables.

- **Unity's version must match the game's exactly.** The game's is the ProductVersion of `UnityPlayer.dll` in the
  game folder (6000.0.75f1, changeset 26349cd2a5c8). When a game update changes it, install the new editor, then
  update `unity/ProjectSettings/ProjectVersion.txt` and the default in `build.ps1`.
- **Unity Hub from a VS Code terminal:** VS Code sets `ELECTRON_RUN_AS_NODE=1`, which makes the Hub run as plain
  Node. Clear it first:
  `Remove-Item Env:ELECTRON_RUN_AS_NODE; & "<Hub folder>\app\Unity Hub.exe" -- --headless install --version <v> --changeset <c>`
  (the Hub folder is `(Get-AppxPackage UnityTechnologies.UnityHub).InstallLocation`).
- **Licence:** the batch build ran without signing in. If it ever stops with a licensing error, sign in to Unity
  Hub once and add the free Personal licence (Preferences, Licenses).
- **Blender downloads:** download.blender.org refuses scripted downloads (403); the official mirrors work, for
  example `https://mirrors.ocf.berkeley.edu/blender/release/`. Check the `.sha256` file.

### Live editors over MCP

Whatever is made through them follows the workshop's rule: it is saved in this workshop (`assets/<name>/`, the Unity
project here), never inside a mod's folder, until the user decides to release it in a mod (see "Assets stay here until
they ship").

An open Blender or Unity window can also be driven through MCP servers registered in the workspace root's
`.mcp.json` (untracked, local to this machine), both run by `uvx` with `DISABLE_TELEMETRY=true`. They are for looking
around, measuring and trying poses quickly; what ships is still built by the scripts here, so anything worked out
live goes back into `model.py` or the editor scripts.

- **Blender** (`blender`): PyPI `mcp-for-blender`, talking to its add-on in a GUI Blender. `--factory-startup` builds
  never load the add-on.
- **Unity** (`unity`): MCP for Unity (CoplayDev, MIT), pinned to v10.2.0 in two places that must match: the package
  `com.coplaydev.unity-mcp` in `unity/Packages/manifest.json` and `mcpforunityserver==10.2.0` in `.mcp.json`. It runs
  over stdio: in the editor, **Window, MCP for Unity**, set the transport to **stdio** once (the package defaults to
  HTTP, and the setting is per user, not per project), and leave its **Configure** buttons alone, since the client
  config is `.mcp.json`. The bridge stays off in batch mode, so `build.ps1` is unaffected, but batch builds still
  cannot run while the editor has the project open.
- The Unity server's `generate_model`, `generate_image` and `generate_audio` tools, and the Blender add-on's Sketchfab,
  Hyper3D and similar downloads, are cloud AI generators or third-party stores: their output's licence is unclear or
  varies per item, so none of it goes into a released mod. Poly Haven and ambientCG (CC0) are fine.

## Assets stay here until they ship

Every asset is made and kept in this workshop, never in a mod's folder: its source in `assets/<name>/` (effects in
`vfx/effects/`, sounds in `sfx/sounds/`), its builds and bundles in gitignored `out/` folders. It goes into a mod (the
bundle copied into the mod's `assets/bundles`, the code that uses it) only when the user decides to release it in that
mod, in the same change as the release. Concepts, trials, demos and rejected versions never enter a mod, so nothing
unreleased is pushed with a mod or left in it as dead code or dead assets. The `-Install` / `--install` flags of the
build scripts are for that release step only. Before that, an asset is
seen, fought and balanced in the game through `AssetLab` at the workspace root: a gitignored, dev-only mod whose
benches embed bundles straight from these `out/` folders (`AssetLab/README.md`).

## The codex: building to the game's look

`codex/` holds what the game's own assets measure, from its files: budgets, texel densities, paint, palettes, shaders,
rigs, effects and sounds per kind of asset (`codex/README.md`; `codex/look.md` first). A new asset starts with
`codex/BRIEF.md`, copied to `assets/<name>/BRIEF.md` and filled from the codex, and is checked against it with the tools
below. Each is opt in: an asset that uses none of them builds exactly as before. The local skill `valheim-asset`
is the order to use them in.

    .\build.ps1 -Asset <name> -Lineup      build; then style check (CATEGORY), variants (variants.json), lineup

`-Asset` also takes an asset folder's path (`.\build.ps1 -Asset codex\tools\demo_asset`, the tools' own test: an
iron-banded round shield painted with the recipes, four regions, three variants).

- **Paint recipes.** `blender/workshop/paint.py` has one recipe per material family measured in the codex
  (`paint.make("wood.planks", "face", tint="#5c2c20")`, `paint.iron("blade")`, `paint.linen("banner", dye="red")`,
  `paint.skin("hide", preset="troll")`; `codex/paint.md`). Each sets the material's paint region and family. With the
  recipes set `AO_STRENGTH = 0.2` (they paint their own hollows) and `NORMAL_FROM_ALBEDO` to the family's strength
  (2 to 4; 5 to 7 for bone, bark and stone): the game's normal maps are the albedo's own relief
  (`paint_normal.from_albedo`). `python codex/tools/paint_check.py` checks the recipes against the game.
- **Style check.** `CATEGORY = "shield.round"` in model.py names a category key in `codex/data/*.json`; after the build
  `codex/tools/stylecheck.py` writes `out/style_report.txt`: triangles, texture size, texel density (from the built
  mesh), longest side, the whole albedo, and each paint region against the game's paint family (median value and
  saturation, value span, blotch size, local contrast), each PASS inside the game's range, LOW or HIGH outside it, then
  the three game samples nearest the asset. Blotch size and local contrast catch fine noise where the game paints big
  soft patches. Standalone: `python codex/tools/stylecheck.py assets/<name> [--category <key>]`.
- **Paint regions.** A material names the part a mod may recolour: `regions.mark(mat, "metal", family="metal.iron")`
  (the recipes do it themselves). Names with fixed mask colours are in `regions.COLOURS` (primary, secondary, trim,
  metal, leather, cloth, skin, glow, bone, wood, stone, fur, rope, gem); unmarked materials fall into `base`. When any
  material has a region the build also bakes `<name>_regions.png` (the albedo's UVs and margin, one flat colour per
  region, no texel blending two) and the manifest lists the regions (name, mask colour, share, median colour, family).
- **Recolour variants.** `python codex/tools/recolour.py assets/<name>` writes `out/<name>_albedo_<variant>.png` for every
  variant in `assets/<name>/variants.json` (`{"variants": {"blue": {"primary": "hue:+0.6,sat:0.85"}, "bronze":
  {"metal": "tint:#9a6a34"}}}`) and `out/variants.png`, each variant rendered on the model. `hue`, `sat` and `val` shift
  a region; `tint:#rrggbb` moves the region's median colour to the tint while each texel keeps its brightness against
  the median (baked light, grime, occlusion) and `keep` (0 to 1) of its own hue: a tint can colour grey iron bronze,
  a hue shift cannot. `codex/recolour.md` compares this with the game's own shader HSV and style textures.
- **Lineup.** `blender --background --factory-startup --python blender/lineup.py -- --asset <name> [--category <key>]
  [--refs A,B | --refs +Skeleton]` renders the built asset beside its category's reference prefabs, point filtered,
  under one sun and sky, on dark earth: `out/lineup/front.png`, `turn.png`, `close.png` and `lineup.blend` (open it with
  `blender <asset>/out/lineup/lineup.blend --python blender/lineup.py -- --open`). Item prefabs lie flat, so the lineup
  stands them on end.

## Making an asset

1. Create `assets/<name>/model.py` with a `build()` function. The folder name becomes the prefab name the game
   registers, so it must be unique across every mod a player might run: prefix it with the mod
   (`openkeep_supply_crate`). `test_crate` is only the pipeline test.
2. `.\build.ps1 -Asset <name>`. This builds, bakes and exports in Blender and renders `assets/<name>/out/preview.png`.
   Look at the preview, adjust `model.py`, repeat. A build takes a few seconds.
3. `.\build.ps1 -Asset <name>,<other> -Bundle <bundle>`. This also builds the bundle
   `out/bundles/<bundle>.windows` and `out/bundles/<bundle>.linux`, one prefab per asset: a bundle only loads on the
   platform it was built for, and dedicated servers run the Linux player. Bundle names are lower-case.
   `-SkipBlender` bundles what is already in each `assets/<name>/out` (an asset built by a script of its own, such as
   `assets/kraken_beak_set` and `assets/kraken_food`, which write the `ecp_kraken_*` loot assets' `out` folders).

### Conventions

- Metres, Z up. The model's **front faces -Y** in Blender and becomes Unity's +Z (forward).
- The **world origin is the pivot**: put it on the ground under the object, where the game places it.
- Every mesh in the scene is joined into one visual mesh with one material. Meshes named `col_*` are colliders:
  `shapes.collider_box` gives a BoxCollider, and `shapes.collider_mesh(obj, convex=...)` a MeshCollider. Anything
  with a Rigidbody (a dropped item) needs box or convex colliders.
- A model may set `TEXTURE_SIZE` (default 512), `NORMAL_MAP` (default True), `AO_STRENGTH` (0 to 1, default 0.6),
  `NORMAL_FROM_ALBEDO` (a strength; default off) and `CATEGORY` (a codex key; see "The codex").

### Helpers

- `shapes`: `box`, `cylinder`, `sphere`, `collider_box`, `collider_mesh`. Sizes are in metres and scale is applied,
  so bevels stay even.
- `materials`: `flat`, `wood` (grain along an axis), `iron`, `stone`. Their fine noise reads unlike the game: prefer
  `paint` (see "The codex").
- `paint`, `regions`: the codex's paint recipes and paint regions (see "The codex").
- Plain `bpy`/`bmesh` code works too: the pipeline takes whatever meshes are in the scene. Any Principled BSDF node
  setup is baked, including image textures (for example CC0 textures from ambientCG or Poly Haven).

### What the Blender step writes (`assets/<name>/out/`)

- `<name>.fbx`: the joined visual mesh plus the `col_*` colliders.
- `<name>_albedo.png`, `<name>_normal.png`: one baked atlas each. Albedo includes the baked ambient occlusion.
- `<name>.json`: the manifest the Unity step reads (files, colliders, size, triangles).
- `<name>.blend`: open it in Blender to look around or edit by hand.
- `preview.png`: four views in one sheet. Top left: three-quarter view. Top right: front. Bottom left: right side.
  Bottom right: next to a 1.8 m figure (a player) for scale. Each view is also kept as `view_*.png`.

### What the Unity step does

`unity/Assets/Editor/BundleBuild.cs` imports each staged asset and builds `<asset>.prefab`: the visual mesh with a
Standard-shader material carrying the baked textures, and colliders in place of the `col_*` meshes. It then builds
the bundle for StandaloneWindows64 and StandaloneLinux64. It logs each prefab's size and fails when it differs from Blender's, which
catches unit and axis mistakes. The full log is `out/unity.log`. An asset's `<name>_icon.png`, when its `out` has one,
goes into the bundle as a sprite named `<name>_icon` (an item's inventory icon: `bundle.LoadAsset<Sprite>(...)`).

## In a mod: BundlePrefabs

`ValheimModLibs/BundlePrefabs` loads the bundle a mod embeds (one per platform: a bundle only loads on the platform it
was built for, and dedicated servers run the Linux player), copies game prefabs to build on, registers new prefabs in
ZNetScene and ObjectDB on every peer, and swaps a model's bare placeholder materials for the game's own. Nothing of the
game's is ever in a bundle: the game's shaders and textures come from the running game.

From the reference export: building pieces (the wood chest's `woodchest.mat`) use `Custom/Piece`, which reads
`_MainTex` and `_BumpMap`; the simplest way to wear the game's look is to borrow a game material outright
(`GameMaterials.Borrow`), as the mimic does for its teeth (Bone Fragments) and gums (bear meat).

## Creatures

A creature is a rig with parts hung on its bones, animation clips, and an animator controller the game's creature code
can drive. `assets/crypt_mimic/` is the worked example:

| File | Does |
| --- | --- |
| `rig.py` | the skeleton, and the game's chest (reference, preview only) hung on its bones |
| `parts.py` | teeth, gums, tongue, eyes, throat, on the game's textures via `reference.material` |
| `poses.py` | named pose controls (lid, teeth, lean, lift, ...) written as complete keys |
| `clips.py` | the clips the game plays (sleep, ambush, idle, walk, lunge, stagger, death), with the bite frames |
| `showreel.py`, `build.py` | a staged fight from the clips; the `.blend` to open, `--render` for video and sheets |
| `actors.py`, `fight.py`, `combat.py`, `hud.py` | the full choreographed fight, rendered with a Valheim-style HUD |
| `export_unity.py` | the FBX (our parts only, rig turned to face Unity's +Z) and its manifest |
| `unity_fight.py` | the fight as JSON for the Unity replay |

```
blender --background --factory-startup --python assets/crypt_mimic/build.py -- --render     showreel + sheets
blender --background --factory-startup --python assets/crypt_mimic/export_unity.py           FBX for Unity
python assets/crypt_mimic/unity_fight.py                                                     fight for Unity
(copy out/unity/* to unity/Assets/Creatures/crypt_mimic/, then)
Unity -batchmode -projectPath unity -executeMethod Workshop.CreatureBuild.Run -workshopCreature crypt_mimic
Unity -batchmode -projectPath unity -executeMethod Workshop.CreatureBundle.Run -workshopCreature crypt_mimic
      -workshopBundle ecr_cryptmimic -workshopOut out/creature_bundles
Unity -batchmode -projectPath unity -executeMethod Workshop.PreviewShots.RunFight -workshopOut out/fightshots
Unity -projectPath unity -workshopOpenPreview      opens the preview scene and plays the fight
```

- The animator controller (`CreatureAnimator`) uses the parameters the game sets: `forward_speed`, `sideway_speed`,
  `turn_speed`, the `sleeping`, `dead`, `onGround` (and similar) flags, the `stagger` trigger and one trigger per attack
  clip. Attack states carry the tag `attack` and bite frames get an `OnAttackTrigger` event.
- Facing: an armature built facing -Y faces -Z in Unity, so `export_unity.py` bakes a half turn into the rig (bone rest
  poses and parts, not the armature object); `CreaturePrefab` fails the build if the front bone isn't on +Z.
- **No root motion from Blender rigs.** Clip travel came out backwards (a turned armature: Unity leaves the parent's
  rotation out), then downwards (Unity reads the travel in the moving node's own tilted frame; Blender's exporter gives
  the armature's contents and its motion opposite forwards). Clips are exported without travel and the mod moves the
  creature itself (the mimic's `MimicLeap` follows the clip's forward curve through `Character.AddRootMotion`).
  `RootMotionCheck` plays each attack through the animator and fails the build on any backwards, sideways or vertical
  travel.
- The model and prefab keep bare materials; `PreviewMaterials` textures only the preview scene's copy, and
  `CreatureBundle` fails if anything from `Assets/Reference` would go into the bundle.
- Batch mode cannot open the project while the editor has it open: close the editor first.
- Videos: `blender/encode.py` turns a folder of frames into an MP4 with Blender's own encoder (no ffmpeg needed).
- The game's code finds `Visual`, `Head` (a case-sensitive child search, not the avatar), attack origins and attach
  points by exact name; attack states need the tag `attack`; walk and run clips need a `footstep` float curve;
  parameters a mod adds must be listed in `ZSyncAnimation`. `codex/rigs/README.md` has the whole contract.

## A game creature with new gear: the Greydwarf Slinger

When the new creature is one of the game's own with something added, the game's body, rig and animator stay and
only the additions come from here. `assets/ecr_slinger/build.ps1` is the worked example (Elite Creatures Pack's
Greydwarf Slinger):

| Piece | Made by |
| --- | --- |
| `assets/ecr_slingshot`, `ecr_sling_band`, `ecr_sling_pouch`, `ecr_slinger_satchel` | ordinary `model.py` assets (Blender) |
| the shot clip `ecr_sling_shot` | `unity/Assets/Editor/Slinger/SlingClip.cs`, authored on the game's Greydwarf |
| the kit `ecr_slinger_kit` | `SlingKit.cs`: the parts under mounts named after the Greydwarf's bones |
| stills and a video | `SlingerPreview.cs`: the game's Greydwarf (reference only) wearing the kit |

```
.\assets\ecr_slinger\build.ps1 [-Preview] [-Install] [-SkipBlender]
```

- **Humanoid clips on the game's skeleton.** The Greydwarf's avatar is humanoid, so a new clip is muscle curves the
  game plays through the creature's own avatar; the mod swaps it in for one of the controller's clips with an
  `AnimatorOverrideController`. `SlingPoser` poses the real skeleton (chest turn, two-bone IK for the arms, hand
  aim), `HumanPoseHandler.GetHumanPose` reads the pose back as muscles and `SlingTrack` keys them (RootT/RootQ are
  the pose's body position and rotation). Clip settings are copied from the controller's own clip (the Greydwarf's
  keep their original orientation) and the first and last keys are the idle's first frame, so the blends in and out
  are clean. Only bones the avatar maps can move (the Greydwarf's `spine3` is not one).
- **Game scale.** The reference model prefab is not always at the game's size: the Greydwarf's `Armature.001` is 100
  there and 50 in the game prefab. `SlingerBuild.GameSizedGreydwarf` sets the game's value, so offsets measured on
  the bones carry straight into the game.
- **Kit mounts.** The kit prefab's top-level children are named after bones (`l_hand`, `r_hand`, `root`); the mod
  parents each to the bone of that name with no offset, so every part keeps the offset measured here.
- **Batch mode skins a mesh once.** Rendering several poses in one batch run shows the first pose on every still
  unless each `SkinnedMeshRenderer` has `forceMatrixRecalculationPerRender` on. When a part seems to float, put
  marker spheres on the bones: here they showed the bones were right and the mesh stale.
- The bundle keeps Standard placeholder materials; the mod puts the game creature's own material, wearing the baked
  textures, on every part (`GameMaterials.Dress`).

## Gear fitted to the body: the Rime Giant

Elite Creatures Pack's Rime Giant is the game's forest Troll (the mod scales it 1.4x and tints it) wearing plates of
rime ice, a snow crust while it sleeps, and throwing an ice boulder. The parts are built to the Troll's own body:
Unity measures the body first, Blender builds each part to the measurements, Unity hangs them on the bones.

| Piece | Made by |
| --- | --- |
| the fit `out/rimegiant/fit.json` | `unity/Assets/Editor/RimeGiant/RimeFit.cs`, `RimeCrustFit.cs`: the skin under each plate (idle), the snow line and rime points over the sleeping troll |
| `assets/ecr_rime_plate_0`..`7` | `assets/ecr_rimegiant/rime_plate.py`: a slab whose back follows the measured skin, chunky crystals on top |
| `assets/ecr_rime_crust`, `ecr_rime_crust_1`..`6` | `rime_crust.py`: a low-poly snow blanket, rime crags and icicles, one piece per bone |
| `assets/ecr_rime_boulder` | its own `model.py` |
| the kit `ecr_rimegiant_kit` | `RimeKit.cs`: plates and crust pieces under mounts named after the Troll's bones |
| stills, a video, a clearance log | `RimePreview.cs`, `RimeFrames.cs`, `RimeClearance.cs`: the game's Troll (reference only) wearing the kit |

```
.\assets\ecr_rimegiant\build.ps1 [-Preview] [-Install] [-SkipBlender] [-Measure]
```

- **Pose through the Animator, not `SampleAnimation`.** `AnimationClip.SampleAnimation` writes a humanoid clip's raw
  root rotation (the troll's idle came out facing backwards); `RimePoser` plays each clip through a throwaway
  controller so the clip's own root settings apply, as in the game.
- **The game prefab, not the model prefab.** The Troll's model prefabs differ from `Troll.prefab` (other meshes, avatar,
  armature rotation, scale 100 against 130), so `RimeReference` instantiates the game prefab's `Visual` itself, from
  `Assets/Reference/Troll` (a subfolder: its `Throw.anim` would overwrite the Greydwarf's).
- **Fit, don't guess.** A plate's back is the skin's height under a 21x21 grid over its footprint, sunk 5 cm in; the
  crust's snow is a heightfield of the sleeping troll seen from above. The rays keep to the plate's own bones' skin, so a
  hand hanging in front of a thigh is not measured as the thigh.
- **The crust rides several bones.** The fit logs how far the snowed skin moves against each bone through the sleeping
  loop; each part of the crust hangs from the bone its skin moves least against, and the hands stay bare.
- **Look.** Muted pale cyan-grey ice with painted crack lines, ridge highlights and big facets; white snow in patches
  over grey rime, like the game's own ice and snowy rocks. Parts bake at 256 px; the bundle is about 2 MB per platform.
- `RimeProbe` renders every Troll clip and the developers' unused `FrostTroll` model for reference.

## A creature the mod animates itself: the Kraken

Elite Creatures Pack's Kraken is a sea boss whose code moves every bone itself (no Animator, no clips), so the bundle
is two skinned prefabs whose bones must sit exactly where the code expects them, plus four screen ink splats.

| Piece | Made by |
| --- | --- |
| `ecp_kraken_tentacle`: straight along +Z from 0 to 8 m, suckers on -Y, bones `kt_00`..`kt_15` every 0.5 m and `kt_end` | `assets/ecp_kraken/tentacle.py` |
| `ecp_kraken_head`: face +Z, beak halves, siphon, eyes on their own mesh and material, markers | `head.py`, `head_parts.py` |
| the head's body: mantle, face and a muscular column down to y -5 on a spine the mod bends (`kh_root` > `kh_body_1..3`; the head leans on it via `kh_neck`), the roots of the six long tentacles wrapped round it | `body.py`, `column.py` |
| the skin: mottling, scars, underside, suckers, beak, barnacles, baked onto the game mesh | `paint.py`, `bake_export.py` |
| `ecp_kraken_eye_albedo`, `ecp_kraken_ink_0`..`3` (512 px, straight alpha) | `textures.py` (plain Python, numpy and Pillow) |
| prefabs, the contract check, report, bundle, stills | `unity/Assets/Editor/Kraken` |

```
.\assets\ecp_kraken\build.ps1 [-Preview] [-Install] [-SkipBlender]
```

- **JSON, not FBX.** Blender writes each piece as JSON in Unity's axes (`(x, y, z) = (-bx, bz, -by)`, triangles
  rewound for the mirror): vertices split where normals or UVs differ, up to four bone weights, the rig and its markers
  at their rest positions. `KrakenPrefab` builds the bones as plain transforms with identity rotations at exactly those
  positions, binds the mesh to them and adds the SkinnedMeshRenderers; no importer settings stand between the contract
  and the prefab. Unity computes the tangents.
- **The contract is checked.** `KrakenContract` writes out what the mod relies on independently of the Blender side and
  `KrakenCheck` fails the build when a name, parent, rest position (1 mm for `kt_*`) or identity rest rotation is off,
  the tentacle does not lie along +Z with its suckers and pale underside on -Y (vertices standing out of the skin, the
  albedo under the downward normals), the beak and `kh_mouth` are not on +Z, a renderer has no bones or bindposes or a
  culling box too small for its mesh, or the skin does not follow the bones (it curls the tentacle, opens the jaws and
  leans the neck, then bakes the mesh), or the column below y -1 moves when `kh_neck` leans 40 degrees either way.
  `KrakenBodyCheck` bends the column 25 degrees at each spine bone about X and Z: no edge may stretch more than 0.3 m,
  the lower end must ride `kh_body_3`, nothing above y -0.8 may move, and the culling box must hold the column bent
  3 x 30 degrees any way with the neck at 0 or 40 degrees either way. Every
  run writes `out/bundles/ecp_kraken/report.txt`: the transform trees, bounds, triangles, radii, textures and sizes.
- **Baked from a detail mesh.** Each piece is built twice: the game mesh, and a bake mesh with every sucker (the
  tentacle models only the big ones; the column's tentacle roots none) and the paint as a node tree on corner attributes. Cycles bakes
  selected to active, colour with the mesh's own ambient occlusion and a normal map. The jaws are baked open, so their
  insides and the throat get their own paint. The tentacle unwraps along seams in four lengths; the head smart-projects,
  with the beak, lips, lids and siphon blown up first so they get more of the atlas, and the column (always half under
  water) shrunk so it gets less.
- **The neck and the spine.** Below y -0.8 the column hangs on `kh_root` > `kh_body_1` (y -1.0) > `kh_body_2` (-2.4) >
  `kh_body_3` (-3.8), blending linearly from one to the next (whole on `kh_root` at -0.8, `kh_body_1` at -1.7,
  `kh_body_2` at -2.9, `kh_body_3` from -3.8 down), so the mod can arc it from a head lunging over a ship's rail down to
  the water outside the hull. From -0.8 up the skin blends into `kh_neck` (whole at 0.6; the pivot is at 0.8), so the
  head bends on top of it. Every vertex takes the weights of its own height (the roots' rings are tilted), at most two
  bones each. The stills show `kh_neck` at +35 and -18, the spine at 25 degrees per bone, and the lunge.
- **Preview scenes.** The game's longship and Karve come from the reference export with every mesh, material and texture
  they reference (found by GUID in the export's .meta files, copied with their GUIDs into `Assets/Reference/KrakenShip`).
  The game's float box is centred on a ship's origin (`Ship.FixedUpdate`), so the stills float it there: longship deck
  floor 0.64 m and rail 1.33 m above the water, Karve 0.01 m and 0.73 m. The head is shown in the attack pose (base 0.6 m
  under water, raised 2.5 m) and merely surfaced. Tentacles are laid along curves bone by bone in world space, as the mod
  does. The ship prefabs sit at y 51 in the export: put them back at the origin.
- **Gotcha:** the ship's LODGroup lists the hull in LOD0 and LOD1; hiding every LOD1 renderer hides the hull.
- Look: murky crimson-brown to dark purple, darker blotches and cell lines, pale healed scars and sucker rings, a pale
  cream-pink underside, pale sucker rims round dark pits, a near-black chitin beak with an amber base, pale amber eyes
  with a black slit, barnacles on the mantle and the column. Head 10.6k triangles plus 0.8k for the eyes, tentacle 2.8k;
  about 3.6 MB per platform.
- History: the first version (2026-09-28) had a 12.8 m tentacle and eight short arm stubs round the crown; in game the stubs
  looked bad under the attacking head, so they gave way to the column, and the tentacles were halved.

## A game creature with clips of its own: the Skeleton Crossbowman

Elite Creatures Pack's Skeleton Crossbowman is the game's Skeleton (its body, avatar and controller) with a crossbow of
bones, its string, its bolts and a bolt quiver from here, and five humanoid clips authored on the Skeleton that the mod
swaps in for the controller's own: its idle, walk and run (carrying the crossbow) and the archer's two bow states (raise
and aim, then fire and reload).

| Piece | Made by |
| --- | --- |
| `assets/ecp_xbow_crossbow`, `ecp_xbow_string`, `ecp_xbow_quiver`, `ecp_xbow_bolt` | ordinary `model.py` assets (Blender); the points the code uses are their constants |
| the clips `ecp_xbow_aim`, `ecp_xbow_fire` | `unity/Assets/Editor/Crossbow/XbowClips.cs` (the keys), `XbowStance`, `XbowRightHand`, `XbowAuthor` |
| the clips `ecp_xbow_idle`, `_walk`, `_run` | `XbowCarry.cs`: the game's own clips frame by frame, both hands put on the crossbow |
| the kit `ecp_xbow_kit` | `XbowKit.cs`: crossbow under LeftHand, the right hand's pinch under RightHand, the quiver under Hips, a bolt in the groove, the pinch and each quiver slot |
| a playback check | `XbowCheck.cs`: every key and seam as the Animator plays it, logged |
| stills and a video | `XbowPreview.cs`, `XbowFrames.cs` (through `XbowGameAnimator`, a copy of the game's bow states), `XbowRigPreview.cs` |
| the players' Bone Crossbow | `XbowItemModels.cs`: `ecp_xbow_item_unloaded` (string let go) and `_loaded` (spanned, a bolt in the groove), their origin at the fore-stock where the game's crossbows sit on the left hand; `assets/ecp_xbow_crossbow/icon.py` renders `ecp_xbow_crossbow_icon` (256 px, spanned, top view), bundled as a sprite |

```
.\assets\ecp_crossbowman\build.ps1 [-Preview] [-Install] [-SkipBlender]
```

- **Pose through the Animator, several posers on one skeleton.** `XbowPoser` plays each clip through a throwaway
  controller (as `RimePoser` does); each poser has its own controller asset and puts it back before it poses, so the
  base-pose poser and the played-clip poser can share the skeleton.
- **Keys from the idle's first frame.** Every key starts from it: the chest turns and bows, the head turns back and
  lays on the stock, the crossbow is placed and the left arm solved (two-bone IK) to hold it, then the right arm does
  its task. The pose is read back as muscles (`HumanPoseHandler`), fingers are closed by the key, and the clip gets the
  idle's settings, except **`keepOriginalPositionXZ = true`**: the idle bakes the root on the centre of mass, which moves
  with the arms, and shifted the whole fire clip by 2 cm (its first key's arms are up).
- **Hold the crossbow where the wrist is easy.** The first try hung it in the right fist the way the game hangs a
  sword (`RightHand_Attach`'s +Z): aiming it level then needed forearm twists and wrist bends past 1.8 times the
  muscle limits, and interpolating between two such keys swung the crossbow 55 degrees off at the shot.
  `XbowAuthor` logs every arm muscle past its limit; the fix was to hold it like the archer's bow, in the left fist
  round the fore-stock (the stock along the fist's little-to-index line, the groove on the palm side), with the right
  hand free for the wrist and the reload.
- **Twice, for the fingers.** The right hand's pinch (between the curled index and middle fingertips) is guessed from
  the idle, the clips are authored, the pinch is measured on the played clip at the lay (where its bolt lies in the
  groove), and the clips are authored again with it. The kit's `ecp_xbow_pinch` is that measurement.
- **Re-authored locomotion.** The Skeleton walks with the archer's shield walk, which swings the left hand across the
  crotch: a crossbow there cut through the legs. `XbowCarry` keeps the game's idle, walk and run for the legs and body
  and puts both hands on the crossbow at a low ready whose place rides `Spine2` and whose aim stays fixed in the
  creature's frame (riding the chest's rotation tipped it at the ground in the hunched walk). The shot clips start and
  end on the carry's first frame, so the seams are 1 cm.
- **Reach.** The let-go string's middle is 0.4 m past the grip; the lowered crossbow for the reload is placed and the
  back leaned 25 degrees so the right hand reaches it (the check logs how far the pinch misses each point).
- **What the check prints.** Crossbow off by per key (0.0 cm when the avatar plays it faithfully), the aim at the shot
  (0.9 degrees off level), the pinch against the string's rest, the nut, the quiver's bolt and the groove, and the
  seams between the carry and the shot clips.
- **In Blender.** The clips live in Unity (humanoid muscles), so Blender gets them baked: `XbowBlenderBake` plays the
  carry idle, the attack through the game's shot states, the walk and the run, and `XbowCache` writes every renderer's
  mesh and a `.pc2` point cache of its vertices per frame; `assets/ecp_crossbowman/blender_scene.py` builds
  `out/blender/crossbowman_animations.blend` (Mesh Cache modifiers, textures, markers naming each part of the timeline)
  and `blender_play.py` opens it playing. The Animator must be set to always animate there: with no camera it culls
  and the skeleton stays in its T-pose. After a `build.ps1` run:
  `Unity -batchmode -projectPath unity -executeMethod Workshop.Crossbow.XbowBlenderBake.Run -workshopOut assets/ecp_crossbowman/out/blender`,
  then `blender --background --factory-startup --python assets/ecp_crossbowman/blender_scene.py`, then
  `blender assets/ecp_crossbowman/out/blender/crossbowman_animations.blend --python assets/ecp_crossbowman/blender_play.py`.
  The bake also writes `tracks.json` (`XbowTracks`: the crossbow's and every bolt's world matrix per frame, in
  Blender's axes); `blender_scene.py -- --compare` uses it (`blender_compare.py`) to put a second skeleton beside the
  first doing the same animation with another crossbow and its bolt (the Gravebranch set in
  `assets/bone_crossbow_set`), its grip moved onto ours, and saves `crossbowman_compare.blend`.
- **Bolts in a narrowing quiver.** The case narrows from 11 x 7 cm to 8 x 5; bolts standing straight or splayed near
  its walls cut through them lower down, and the game's bone bolt (10 cm vanes at our size) reached below the rim.
  `XbowParts.QuiverSlot` stands each bolt 14 cm out, aims its shaft at a point near the bottom's middle and turns it
  about its line so the vanes interleave; the bolt of our own has 3.2 cm vanes.
- Look: bones like the game's (`LargeBone`, `BowSpineSnap`, `ShieldBoneTower`): a femur stock, vertebrae along the
  fore-stock (bowed 2.2 cm down in the middle, each leaning with the bow, threaded on a center bone), the femur 15 cm
  behind the grip, rib prod, jawbone stirrup, beige mottled brown with sinew lashings; the bolt grimed the same way with a blunt knuckle-bone head and
  dark feather vanes; stiff brown leather quiver with a bone cap. About 1.2 MB per platform.

## A new body on a game skeleton: the Mossback

A completely new creature body modelled in Blender on an exact copy of a game creature's skeleton, so the game's own
avatar, animator controller and clips play it unchanged; a mod puts it on a copy of that creature
(`BundlePrefabs.CreatureBody`). `assets/workshop_gamerig_demo` is the worked example: a hulking forest ogre on the game
Skeleton's 53 bones, 3,808 triangles, 256 px (route (b) of `codex/rigs/README.md`).

| Piece | Made by |
| --- | --- |
| the rig, an exact copy of the game prefab's skeleton | `blender/workshop/gamerig.py`; `gamerig_source.py` reads the prefab, `gamerig_codex.py` cross-checks `codex/data/rigs.json` |
| weights | `gamerig_weights.py`: `auto` (heat, chosen bones), `rigid`, `fixed`, `blend`, `chain`, `region` (soft ellipsoid), `smooth`, `finish` |
| body, parts, paint, sockets | `assets/workshop_gamerig_demo/model.py`, `body.py` (metaballs grown round the bones, decimated by region), `parts.py`, `looks.py` |
| join, bake, contract | `gamerig_build.py` (`gamerig_run.py` is the command line), `gamerig_export.py` |
| prefab, checks, playback, bundle, stills, video | `unity/Assets/Editor/GameRig` (`Workshop.GameRig.GameRigBuild`) |
| sheets, Blender scene | `gamerig_sheet.py`, `gamerig_scene.py` |

```
.\assets\workshop_gamerig_demo\build.ps1 [-Preview] [-Blend] [-SkipBlender]
```

A model.py for this route:

```python
from workshop import gamerig, gamerig_weights
SOURCE = "Characters/Skeleton/Skeleton.prefab"   # the game prefab the mod copies, not a model prefab
CATEGORY, TEXTURE_SIZE, SMOOTHNESS, BONE_ORDER = "creature.humanoid", 256, 0.18, "game"   # or "own"
def build():
    rig = gamerig.armature(SOURCE)
    # meshes placed from gamerig.head(rig, "LeftForeArm") ...; materials from paint.* with regions
    gamerig_weights.auto(body, rig); gamerig_weights.region(body, centre, radii, {"Head": 1.0})
    gamerig.socket(rig, "Mouth", "Jaw", (0, -0.235, 1.735))
    return rig          # every other mesh left in the scene is part of the body and must be weighted
```

- **The copy.** `gamerig.armature(SOURCE)` reads the game prefab, not a model prefab: every bone, socket and end below
  the armature, with the game's names and parents. Each Blender bone's head sits at the game's rest position and its
  axes are the game bone's own; one fixed turn per skeleton is detected automatically (Unity Y on the Skeleton, Troll
  and Greydwarf, -X on the Wolf and Boar), and `gamerig.frame` gives the game axes back. The armature stays at scale 1
  in metres. The game's armature transform (the Skeleton's 100 under 0.95, turned -90 degrees about X) goes into the
  contract, written from the prefab's own numbers. Checked in Blender on seven rigs (Skeleton, Greydwarf, Troll, Wolf,
  Player, Boar, Lox); built through Unity on the Skeleton.
- **JSON, not FBX.** As the Kraken: the mesh in root space and Unity's axes, four weights a vertex in the game body's
  bone order (`BONE_ORDER = "game"`, so the game's `attach_skin` gear fits; `"own"` otherwise), plus the game's
  transforms with their exact local values. Unity rebuilds the hierarchy from those numbers, puts the
  SkinnedMeshRenderer at the game body renderer's own place and computes the bind poses there. The contract carries the
  codex manifest keys, so `codex/tools/recolour.py` and `stylecheck.py` work on it.
- **Checked against the game, not against Blender.** `GameRigCheck` compares every transform with the game prefab as
  Unity imports it (exact), the bone order with the game body's, and head, hands, feet and facing with the game body at
  rest. `GameRigMotion` plays our prefab (given the game's avatar and controller) beside the game creature through the
  controller's own states (wakeup, idle, walk and run at its blend-tree speeds, every attack trigger, stagger): bones
  identical (0.000 mm), no tearing (no edge past 5x its rest length or 25 cm longer; bulky bodies reach 3 to 4.5x at
  crotch and armpit extremes), feet within 6 cm of the game body's lowest point. `report.txt` names the worst edge.
- **Previews.** The game creature is staged by GUID and our body put on with `GameRigSwap`, the editor twin of
  `CreatureBody.Wear`: `out/unity/sheet_pair.png` (the game body beside ours, state by state), `mossback.mp4` and, with
  `-Blend`, a point-cache Blender scene. The game's controller is copied under its own GUID into
  `Assets/Reference/GameRig`, so the staged prefab keeps no controller for other tools.
- **Gotchas.** A creature that shares a rig's names need not share its lengths: the Skeleton has the player's 53 names
  and order but stands in a T-pose at 1.99 m, spine 12 cm higher, hips 28 cm wide; build on the prefab the mod copies.
  Metaball thighs merge at the crotch unless kept 15 cm apart. Hard box selections of a rigid head or jaw tear the neck:
  use `region`, an ellipsoid fading into the heat weights, then `smooth`. Blender's Decimate vertex group protects
  weight 0, not 1: decimate selections with `mesh.decimate` instead.
- **Look.** Grey-green skin painted light (tints and star shifts colour it), moss on the hump, leather wraps, a ragged
  hide loincloth, ivory tusks and claws, pale yellow eyes; regions skin, fur, leather, cloth, bone, glow; `variants.json`
  frost, swamp, ash. The metaball body is smoother and softer than the game's faceted creatures: it proves the rig
  route, not the look.
- Not done: not seen in game, no mod code, no death effect or ragdoll of its own, no `attach_skin` gear tried on it.

## A fight keyed in Blender: the Deathsquito Queen

The game's Deathsquito grown 2.5 times, crowned and gravid, with seven moves of the user's (her `MOVES.md`), her eggs
and needle (`ecp_queen_egg`, `ecp_queen_egg_burst`, `ecp_queen_needle`), and the whole fight previewed in Blender with
the game's own sounds. Workshop preview only (user, 2026-09-29): no mod code yet.

```
.\assets\ecp_deathsquito_queen\build.ps1 [-SkipModels] [-SkipSounds] [-Stills] [-Render] [-Open]
```

- **Rigid parts on a game skeleton, no weight painting.** Her parts are rigid, so `model.py` writes each part's bone of
  the Deathsquito's skeleton onto its faces (an integer face attribute, `queen_bones.py`); it survives the pipeline's
  join and bake, and `queen_rig.py` turns it into vertex groups of weight 1 on a `gamerig` copy of the skeleton scaled
  2.5 and applied. Parts that must bend with the game's chains are laid out on its joints (her legs on the Deathsquito's
  one leg chain a side).
- **Moves as maths.** Each move is a class giving her state at any moment (place, turn, pitch, bank and a few pose
  numbers) and its events (sounds, hits, needles, eggs, waves); `queen_pose.py` turns a state into the root's place and
  the game bones' rotations about her own axes (`rest⁻¹ · turn · rest`), so the same numbers can drive the bones in a
  mod. Every frame is keyed and the curves filled in bulk (`queen_keys.py`).
- **Swinging legs from springs.** Each leg (three a side: the game Deathsquito's one chain carries the middle legs; the
  front and hind get chains of their own, copies of its bones moved along her body, which `CreatureBody` adds to the game
  creature by name) is a few damped springs driven by her acceleration and speed in her own frame (`queen_sway.py`), each
  leg with its own stiffness and idle sway. Cap what braking and backwards flight do: a hard turn at speed read as flying
  backwards and threw the legs up over her back.
- **Effects the user judged** (on an air-pulse attack since removed): a flat curved sheet of air was "basic"; bright
  white rings read as solid donuts (the game's air is faint and dusty); grass bits as flat colour cards read as "green
  square block things".
- **Moves say what the body means, springs do the rest.** A move sets intents (`brace`, `reach`, `spread`, `tuck`)
  and the springs carry the legs there and swing them round it; the abdomen and her posture are springs too, so a
  lunge that jerks the body swings the abdomen and jolts the braced legs without keying either. Legs that only
  dangled read "like a child dangling its feet off a cliff" to the user.
- **Wings buzz by aliasing.** One stroke a frame at 30 frames (15 a second) with EEVEE's motion blur on draws the blurred
  fan of an insect's wings; a stroke meant to be seen needs about four frames.
- **Cameras fit what the shot holds**: her path with her wingspan round it, her target and whatever else (eggs, the other
  player), seen from the side of her line of attack, her front three-quarters, over the target's shoulder or from high.
- **Gotcha:** piping Blender's output into PowerShell's `Select-Object -First` stops Blender once enough lines have come:
  a render quits early without an error.

## A weapon posed on a game creature: the spine greataxe

A two-handed greataxe for a skeleton, all bone, 2.13 m long with a head a metre across: the haft is a whole spine
(tailbone, sacrum, 4 lumbar, 8 thoracic, 4 cervical vertebrae, chunky like the Spinesnap's) in an S-curve in the
blade's plane, spines along the back, two leather grips, sinew bindings; it rises into a plain skull with a rawhide
band, a giant's jawbone for a bearded crescent set against one temple on the boss of its joint, its square ivory teeth
(two long fangs, one broken) the cutting edge, a tusk through the other temple for the back hook and a fang in the
crown. An ordinary `model.py` asset (no rig or clips yet), plus a Blender scene of the game's own Skeleton holding it.

| File | Does |
| --- | --- |
| `model.py` | the entry: materials, spine, head; the origin moved to the lower grip (`LOWER_GRIP`, `UPPER_GRIP` are arc lengths) |
| `axe_spine.py` | the centre line (`Centre`: point and frame at any arc length), vertebrae by region, discs, sacrum pommel, grips |
| `axe_skull.py`, `axe_head.py`, `axe_blade.py` | the skull (exact booleans cut the sockets dark), the band, boss, tusk and fang, the jaw (a Coons patch between root, top line, edge and beard; lip, knobbly faces, the ridge that holds the teeth) and its teeth |
| `axe_paint.py` | the paint, as node setups the bake reads (Cycles' AO node darkens the hollows) |
| `showcase_rig.py`, `showcase_pose.py`, `showcase.py`, `showcase_open.py` | the game's Skeleton bound to an armature from its own weights, posed by IK round the axe; the scene |

```
.\build.ps1 -Asset ecp_spine_greataxe
blender --background --factory-startup --python assets/ecp_spine_greataxe/showcase.py -- [--render]
blender assets/ecp_spine_greataxe/out/showcase/greataxe_showcase.blend --python assets/ecp_spine_greataxe/showcase_open.py
```

- **The showcase** has a sentinel (axe planted at its side, a hand on the upper grip), a two-handed guard (axe over
  its right shoulder) and the axe alone on a stone; cameras `camera`, `camera_guard`, `camera_head`. The hand and foot
  targets are empties parented to the axes, so moving an axe in Blender moves the hands with it. A hand closes on the
  haft palm towards the shoulder, thumb towards the head, fingers curled; each IK's pole angle is the one that puts
  the elbow or knee nearest its pole. `--render` writes the three cameras' stills to `out/showcase/`.
- **Load the game's Skeleton once.** A second `prefab.load` of the same skinned prefab reuses the first's posed mesh
  data: it came in at the reference model's 100x armature scale with every vertex group doubled. `showcase_rig` copies
  the first rigged skeleton instead.
- **Looking like the game.** The first versions (21k triangles, a 1024 px atlas of fine procedural noise, thin
  modelled cracks, spirals, a scowling skull, a thin blade) read as another game's art beside the Skeleton. The
  game's bone gear is 600 to 4,000 triangles of few chunky, recognisable parts (vertebrae, skulls, fangs, antlers)
  on 64 to 128 px textures, point filtered and painted: light baked in, big soft blotches, near-black gaps, and on big
  bone surfaces (Bonemass' `bonebone_d`) high-contrast smoky grime over a pitted normal map. So now: 6.6k triangles, a
  256 px atlas, the paint in `axe_paint` (blotches, stains, smoky grime, worn edges from pointiness, light from above,
  AO in the hollows, pitted bump), and no detail smaller than the texture's pixels modelled. A big smooth plate of
  bone reads as cloth whatever its paint; teeth set in its edge make it read as bone.
- Colours from the game's Skeleton, LargeBone, Spinesnap bow and Bonemass bone (ochre-beige, brown hollows,
  rusty-brown stains, smoky grime), the wolf fang and Draugr Fang bow (ivory), the Spinesnap's grip (dark red-brown
  leather); the silhouette after the Skullsplittur and the blackmetal battleaxe.

## Weapons for the game's Skeleton: the skeleton arsenal

The skeleton arsenal is every bone weapon of Elite Creatures Pack: the eight pieces below and the spine (bundle
`ecp_skel_arsenal`), the Bone Crossbow and its blunted bolt (`assets/ecp_xbow_crossbow`, `ecp_xbow_bolt`, bundle
`ecp_crossbowman`), and the Executioner's Greataxe (`assets/ecp_bone_greataxe`, bundle `ecp_headsman`). The last two
share their bundles with the crossbowman and the Executioner, so their own builds make them; the arsenal's
`build.ps1` runs those too, and with `-Install` puts all three bundles into the mod.

A dagger, sword, axe, mace, spear, atgeir, bow and arrow for the game's Skeleton, all made of bone with a vertebra
worked into each, and the spine the skeletons drop (`ecp_spine`, with its icon; it replaced the single vertebra
`ecp_vertebra` on 2026-09-29, kept in the workshop); then a showcase: the game's
Skeletons holding each weapon and doing that weapon's attack, baked in Unity and played in Blender.

| Piece | Made by |
| --- | --- |
| `assets/ecp_skel_dagger` .. `ecp_skel_arrow`, `ecp_spine` | `model.py` each (the spine's `icon.py` too), on the shared code in `assets/ecp_skel_arsenal`: `grave_shapes` (lofts, bands, wraps), `grave_vertebra` (the vertebra, in kinds), `grave_bones` (long bones, jointed hafts, ribs, fangs, pores), `grave_blade` (ground bone blades, the edge marked for the paint), `grave_paint` (bone, vertebra, ground edge, teeth, sinew, hide) |
| the attacks | `unity/Assets/Editor/SkelArsenal`: `ArsenalRoutines` (which game clips, speeds and transition times), `ArsenalAnimator` (a controller per weapon), `ArsenalBow` (string and arrow), `ArsenalBake` (the bake) |
| the showcase | `assets/ecp_skel_arsenal/blender_scene.py` (the .blend), `blender_play.py` (opens it playing), `blender_stills.py`, `present.py` (a sheet of every piece), `review.py` (a piece beside the game's own) |

```
.\assets\ecp_skel_arsenal\build.ps1 [-Open] [-SkipBlender] [-SkipBundle] [-SkipBake] [-Install]
```

- **Into the mod.** `-Install` copies the bundle `ecp_skel_arsenal` (ten prefabs, eight authored icons beside their
  `model.py` and the spine's from its `icon.py`), then the crossbow's `ecp_crossbowman` and the greataxe's
  `ecp_headsman`, into `EliteCreaturesPack/EliteCreaturesPack/assets/bundles`; `-SkipBake` stops after that, without
  the showcase (`-SkipBlender -Install -SkipBake` rebuilds only the three bundles, from the models already built). The mod's `Arsenal/` code puts the
  models on the skeletons, the players' weapons, the arrows and the spine. `ecp_skel_bow_player` is the same bow
  turned into the player's bow hold (measured from the game's Bow), since players hold bows differently from the
  skeleton archer; its tips go to `out/ecp_skel_bow_player_points.json`, copied into the mod's `ArsenalLook`.

- **The game's attach frame.** `VisEquipment.AttachItem` parents an item's `attach` child to the hand's attach point
  at zero position and rotation, keeping its world scale; so each weapon is built with the fist at the origin and the
  weapon up -Y (Unity +Z), measured against the game's own in that frame (`review.py` draws them side by side). The
  spears point the other way (SpearBronze and SpearFlint carry the head at Unity -Z: the spear clips hold it
  overhand); the atgeir lies 21 degrees off the axis like AtgeirIron (so the two-handed clips find the other hand on
  it), and the bow sits slanted in the left fist like skeleton_bow. The bow writes its tips and rest to
  `out/ecp_skel_bow_points.json` for the string and arrow.
- **The game's own attacks.** The Skeleton's sword swing (its `attack_axe` state), Skeleton_Poison's mace swing and the
  archer's `bow_idle` then `attack_bow`; for weapons skeletons never carry, the player's primary attack (knife slashes,
  Javelin Stab, the atgeir and axe combos) through the Skeleton's humanoid avatar. States, clips, speeds and transition
  times are read from the game's controllers. Clips another tool already brought into `Assets/Reference` are found by
  the game's GUID and reused, never copied twice.
- **One loop per skeleton.** Each Skeleton is baked on its own (idle, attack, idle) by the crossbowman's `XbowCache`;
  in Blender its cache frame is keyed as a sawtooth with a Cycles modifier, so every skeleton repeats its attack
  forever with no scripts. The timeline tracks along the row, then cuts to each weapon, then to the dropped spine.
- **Look (v2).** Every weapon is built round a vertebra, not merely carrying one: a vertebra lying flat in the
  blade's plane (`grave_vertebra.flat`), canal showing through, whose spine is drawn out into the blade and whose
  wings become quillons, lugs, barbs, horns or a hook (dagger, sword guard, axe head, spear head, atgeir head, arrow
  head); a club of seven growing, turning vertebrae (mace); columns of them (`grave_vertebra.column`: low-poly
  vertebrae stacked on their axes with discs of cartilage between) for the dagger's and sword's grips, the whole
  hafts of the axe and mace and the upper halves of the spear and atgeir (long tail vertebrae; the lower halves are
  long bones jointed with sinew), down the sword's blade from the
  guard nearly to the point, and along the bow's limbs (like the game's Spinesnap). The columns curve like a spine:
  `grave_shapes.bend` with `arc` bends a finished haft straight through the fist and away towards its ends (spines on
  the outside of the curve), so the grip and the game's hold don't move (axe, mace); the vertebra halves of the spear and
  atgeir shafts wind through S-curves like a real spine, the long-bone halves straight (`wave`: 2.5 to 2.8 cm to either side, one S every 0.7 m, straight
  at the fist and faded out before the head so the blade stays on the weapon's line); the sword's backbone winds one
  gentle S down its straight blade. The shaft vertebrae are hourglass-waisted with long hooked spines and recessed discs, and those
  weapons bake stronger ambient occlusion so the grooves read. Few big pieces, the game's way: each weapon has about 20 vertebrae at most (`column(count=...)` sets how many
  and stretches their bodies to fill the length): spear 16 in the shaft, atgeir 13 + 5, sword 3 + 14, axe 12, mace 7 +
  7, bow 7 a limb. Triangles: spear 6.6k, atgeir 7.1k, bow 6.7k, mace 5.2k, sword 3.8k, axe 3.2k (a first version with
  up to 110 small vertebrae a shaft reached 19k). Bones like the game's and the
  bone crossbow's: cream with brown grain and grime, sinew lashings, dark hide grips, ground edges pale.
- **History.** First built as rusted iron and wood with a vertebra added (the user wanted every weapon of bone); then
  v1, all bone with a vertebra somewhere in each, archived with its sources, builds, showcase and bundle in
  `assets/skeleton_weapons_v1` (its README); then v2 above, where the vertebra is the heart of each weapon.

## A weapon on the game's budget: the bone battleaxe

A two-handed axe built to the game's own battleaxes, measured in the reference export first: they are 1.60 to 1.79 m
long with a 0.4 to 0.76 m head, 188 to 1468 triangles and 64 to 128 px textures, and hold their `attach` point (the
lower hand) 0.20 to 0.30 m above the butt. The earlier bone axes here were 18 to 21k triangles with fine detail, which
is what made them look unlike the game. This one: 1.80 m, head 0.71 m across with the spike, 1210 triangles, a 256 px
atlas, origin at the lower hand 0.28 m above the butt, Z up, blade towards +X.

| File | Does |
| --- | --- |
| `model.py` | the entry and the bake settings |
| `haft.py` | a giant's leg bone (knuckle pommel, femur head and trochanter above the head, a slight bow), leather grip, the rawhide collar lashing the blade on, the tusk back spike |
| `blade.py` | the bearded crescent: outline in three Catmull-Rom chains, a ground bevel along the cutting edge, a rolled rim along the back lines, a raised crown in the middle, a natural hole, a knapped edge with one chip |
| `geo.py` | low-poly helpers: lofts along a centre line (winding checked outward), squashed knobs |
| `paint.py` | the paint as node recipes the bake reads |
| `reference_study.py` | renders the game's greataxes, bone gear and skeletons one by one and logs size, triangles and textures (`out/reference/`) |
| `lineup.py`, `lineup_open.py` | the built axe beside the game's Skeleton and four battleaxes, point filtered, under one light (`out/lineup/`), and a window onto it |

```
.\build.ps1 -Asset ecp_battleaxe_bone
blender --background --factory-startup --python assets/ecp_battleaxe_bone/lineup.py
blender assets/ecp_battleaxe_bone/out/lineup/lineup.blend --python assets/ecp_battleaxe_bone/lineup_open.py
```

- Look: colours sampled from the game's textures (linear RGB): bone from `Skeleton_d` (its 10, 50 and 90 % tones),
  the grip from the Spinesnap's leather, the lashing's red from the blackmetal battleaxe's wrap. Big soft blotches,
  rusty-brown stains, brown towards the bone's ends like the Skeleton's joints, painted crack lines on the blade, a
  paler ground edge like the game's metal edges. The lineup checks the result numerically too: the haft's rendered
  median matches the Skeleton's thigh.
- **Pointiness lifts a thin tube everywhere.** The worn-edge lift (Cycles' pointiness) turned the whole haft its
  lightest tone, since every vertex of an 8-sided tube is convex; thin parts skip it.
- **Loft winding.** Rings built round a tangent run clockwise seen down it, which winds the quads inward; the ambient
  occlusion bake then darkens the whole part. `geo.loft` checks its first quad and flips.
- Not done: no icon, not in a bundle, not seen in game, no clips or mod code.

## Effects

Particle effects are built in `vfx/` from the codex's measurements of the game's own (`codex/vfx/`): an effect is a
spec in the codex's words (`vfx/effects/<name>/effect.py`), its textures come from `vfx/textures.py` in the game's style,
Unity (`unity/Assets/Editor/Vfx`) builds the prefab and one bundle per platform, `vfx/check.py` compares it with the
game's numbers, and a preview renders it beside the game's nearest effect, graded like the game's camera. The bundle
holds placeholder materials only; the mod dresses them in the game's particle shaders at runtime (BundlePrefabs'
`BundleEffects`). Often a game effect cloned and recoloured is the better answer (`codex/vfx/catalogue.md`,
`EffectTint`). See `vfx/README.md`; `vfx/effects/` has three examples (spirit-fire burst, frost impact, ember aura).

    python vfx/build.py <effect> --preview --wait                      style report, sheet.png, <effect>.mp4
    python vfx/build.py <effect>... --bundle <name> [--install <folder>] --wait
    python vfx/build.py --reference Effects/vfx_HitSparks.prefab --preview      a game effect alone, for looking

| Path | Does |
| --- | --- |
| `vfx/` | spec, texture generators, style check, build driver, grading, reference staging, example effects |
| `unity/Assets/Editor/Vfx/` | `Workshop.Vfx.VfxBuild.Run`: prefab from the spec, bundle, batch-mode preview in linear-light copies of the game's particle shaders (preview only, never bundled) |
| `codex/vfx/`, `codex/data/vfx.json` | what the game's effects are made of, measured on 1,201 effects |

## Sounds

New sounds are made from scratch in `sfx/` (plain numpy in the workshop's venv, `out/venv`: numpy, Pillow, soundfile)
and measured against the game's own with the codex's sound pages (`codex/sfx/`, data in `codex/data/sfx.json`). A
sound set (`sfx/sounds/<set>.py`) names each sound's archetype, the game sound prefab a mod plays it through, its
variations and a recipe (`sfx/recipes`: creature voices, impacts by material, creaks, swings, fire loops). `build.py`
renders the variations, levels them so that through the copied prefab they play at the archetype's median in-game
level, writes 16-bit 44.1 kHz WAV and OGG, and writes `report.md` (every number against the archetype's ranges) and a
comparison sheet (spectrograms and envelopes over the nearest game clips). `bundle.ps1` builds the set's clips into
their own bundle for Windows and Linux (`Workshop.Sfx.SfxBundle`, under `out\unity.lock`, refusing any file the
manifest does not list and loading the result back to check every clip by name); a mod plays them through
`BundlePrefabs.SfxPrefabs`. Examples: `rootling` (a creature's idle, alert, attack, hurt and death), `crystal`,
`fire_loop`.

    python -m venv out/venv; out/venv/Scripts/python -m pip install -r sfx/requirements.txt     once
    out/venv/Scripts/python sfx/build.py rootling
    .\sfx\bundle.ps1 -Set rootling          -> out\bundles\ecp_rootling_sfx.windows / .linux

- Sources must be licence-clean: synthesis, the user's own recordings or CC0; never AI audio generators, never the
  game's audio in a bundle (a mod plays the game's own sounds by prefab name instead; `codex/sfx/catalogue.md`).
- **Synthesis makes placeholders only.** The user judged synthesised swings, impacts and cracks "comically bad ...
  cartoon noises" (the headsman boss, 2026-09-29), and the synthesised creature voices pass length, level, centroid
  and flatness yet show clean evenly spaced harmonics where the game's are dense and rough. A shipped sound is a real
  recording: the game's own clips at runtime, chosen for the creature's size and trimmed, pitched and filtered there
  (`SfxPrefabs.Variant` / `Copy`; `assets/ecp_headsman/sfx.py` has worked recipes), CC0, or the user's own. The
  measuring, levelling and compare tools here serve that: pick candidates by band and centroid, level them to the
  archetype, compare their spectrograms with the game's.
- Nobody has listened to the example sounds yet, and none has been played in the game.
- scipy is not used: Windows Application Control blocks its compiled modules on this machine.

## Reference export

`.\rip-reference.ps1` exports the game's assets with AssetRipper into `%USERPROFILE%\ValheimReference`, a Unity
project outside the repository. Use it to look up real sizes, how the game's prefabs are built, and the game's
shader property names (the shaders are exported as dummies that keep their properties). Scripts are copied as the
game's DLLs, not decompiled. Never commit or ship anything from it: a mod uses the game's assets at runtime, by name.

**A whole prefab in a Blender preview:** `workshop.prefab.load("GameElements/Ships/VikingShip.prefab")` builds what the
prefab draws under one empty at the origin, with an empty per GameObject on the way and a mesh object per renderer on
the game's textures (through `reference.mesh` and `reference.material`, plus the game's normals, one material per
submesh, tiling, tint and cutout). Transforms are Unity's in the workshop's axes, so the prefab's forward is -Y.
Skinned meshes are posed by the prefab's bones and kept static, Unity's built-in cube is drawn, and ropes
(LineRenderers, through their `LineAttach` points) become curves. Inactive GameObjects, disabled renderers, lower
LODs, the water mask, shadow blobs, particles and lights are left out (`include_inactive=True` and
`skip=lambda name, layer: ...` change that). Everything is tagged `reference.TAG`. GUIDs are looked up in
`out/reference_guids.json`, built from the export's .meta files on first use (about 3 s; a load then takes 0.2 s).
`blender/prefab_test.py` loads and renders the longship into `out/prefab_test/`: the hull is 21.5 m by 5.5 m, bow on
-Y, keel 0.71 m below the origin, deck floor 0.64 m and rail 1.33 m above it. A mesh whose vertex channels carry flag
bits (the Ashlands longship's hull) does not decode in `reference.mesh` yet; it is left out with a message.
`prefab.load` draws nothing for `InstanceRenderer` clutter prefabs (grass and ground cover);
`codex/measure/environment_render.py` `plant()` shows how to place their mesh.

**Without Blender:** `codex/measure/game.py` reads the same export in plain Python (prefabs as nodes and components,
game scripts named from `assembly_valheim.dll`, mesh headers and arrays, materials, texture sizes, texel density); the
codex's measuring scripts build on it.

## What works well

Hard-surface things: props, furniture, containers, building pieces, weapons and tools, and simple creatures whose
motion can be keyed from poses (a chest that bites, crawlers, floating things). Natural walking gaits come from the
game's own clips: a new body on a game skeleton plays them unchanged (the Mossback). Organic sculpting does not work
well yet: bodies grown from metaballs come out smoother and softer than the game's faceted creatures.
