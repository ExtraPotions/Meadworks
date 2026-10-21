# Original Wildblood raid chest source

Author: expDARE. Source-only asset pack, outside Wildblood and Bestiary checkouts. No product prefab, network object, admission/payment logic or game asset is included.

`chest_source.py` is the editable procedural model recipe. It makes three meshes (body, lid, curved hollow horn), a five-node hierarchy, bell socket and three root collider descriptors, in meters with Unity Y up and +Z front. The original colors and UV atlas are specified there. `raid-chest.svg` is the editable icon; it has only original filled polygons and a transparent background. No Blender, bundle extraction, native binary or game export is needed.

The lid sits at `(0, 0.86, -0.59)` with closed rotation identity. It carries the horn and `horn_mouth`; the socket faces the bell tangent. The rearward hinge clears the body/bands across the full 0 to -100 degree opening. Three root boxes enclose the original closed body, lid and horn. They remain root boxes when open, matching the behavioral contract; opening does not animate those collision envelopes. The later product must place its hover/use component on this model root, ahead of its enclosing native Container. No runtime components are bundled here.

The source interoperability reference is the public raid-chest behavior at ValheimMods `f331465b3ed2551c89decc42ba81565a7b689543`. Names `model`, `lid`, `horn_mouth`, three root boxes and local-X lid motion are contracts. Geometry/proportions, SVG shapes, palette and mesh recipe are original; no upstream chest model, texture or icon was copied. Original art is CC BY-NC-SA4.0; generator/editor/validator code is GPL-3.0-or-later. See `NOTICE.md` and `licenses/`.

## Lightweight source checks

From this directory, with the existing Python runtime:

```powershell
python validate_source.py
python chest_source.py --out build/source --stage
```

Generation writes `build/source/raid-chest.meshes.json`. `--stage` copies only this pack's editor and minimal module manifest into its own `build/unity`, retaining an existing Unity Library. It does not start Unity or touch another project. Outputs must remain under this pack's `build` directory. Tests check numeric geometry, winding/normals, collider enclosure, the opening sweep in one-degree increments, hierarchy/socket direction and repeated-byte determinism. Negative cases reject broken socket parenting, collider loss, reversed winding and displaced hinges. SVG checks reject embedded/external image inputs. No rasterization or model/image rendering occurs in these checks.

## Queued Unity command — not run yet

Use absolute paths. The coordinator schedules one heavy job after native jobs drain; budget 8 GiB and a finite 20-minute timeout for first import/two serial platform builds. No Unity Hub window should hold this project open.

```powershell
$chestRoot = (Resolve-Path .).Path
& 'C:/Program Files/Unity 6000.0.75f1/Editor/Unity.exe' -batchmode -nographics -projectPath "$chestRoot/build/unity" -executeMethod MeadWorks.RaidChest.BuildRaidChest.Build -raidChestSource "$chestRoot/build/source/raid-chest.meshes.json" -raidChestIcon "$chestRoot/raid-chest.svg" -raidChestOut "$chestRoot/build/raid-chest" -raidChestPlatforms windows,linux -logFile "$chestRoot/build/raid-chest-unity.log"
```

The editor creates original mesh/material/atlas assets, rasterizes the original SVG polygons on CPU into a transparent256px icon, validates hierarchy/closed bounds/opening and three root boxes, then builds `build/raid-chest/windows.bundle` and `linux.bundle`. The bundle contains `mw_raid_chest_model.prefab` and the original icon Sprite; it contains no game components. Dependencies must stay under `Assets/RaidChestGenerated` except Unity built-in shader resources. It exits nonzero on missing Linux support, invalid input/geometry, unexpected dependencies or a failed platform build. The report is written only after both requested builds succeed.

Do not use the current Bestiary generic joined-mesh/mesh-only FBX exporter: it would collapse the moving lid and omit the socket. This one-chest JSON adapter uses the established Unity strict bundle-build pattern without changing any Bestiary/shared tool. It is not an event-asset framework.

## Qualification still required

Source checks do not establish Unity compilation, buildability, icon quality, native fit, correct material borrowing or collision/hover behavior. After independent asset/code review, compile/build the isolated project, inspect content/dependencies/platform hashes and render the chest/icon at gameplay scale. Then separately authorize product integration and owned native SP/host tests for fit, lid/horn, interaction, wear and save/reload. No asset-ready, release or install claim follows from source generation.

The source checks are single-process, with a 256 MiB budget; measured timings and identities are in the coordinator handoff. They do not spawn Blender, Unity, renderers or servers.

## 2026-10-15 rendered-art correction

The first Unity previews exposed a solid cap under the open lid and coplanar wood/iron end faces. The corrected recipe has four independent rim rails around an open cavity and a wood floor. Lid end-band exterior faces sit 12 mm outside the wooden ends; semantic validation requires at least 8 mm clearance from those planes. Nine downward triangle-ray probes through the opening must reach the wooden floor. Negative cases restore the solid cap or coplanar bands and must fail. The complete source suite now has seven cases and the model has 892 triangles. Hierarchy, hinge, socket, opening sweep and three root boxes retain their contracts. Rebuild and rendered inspection remain pending for these corrected bytes.
