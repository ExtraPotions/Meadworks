# Bone greataxe

A 1.500 m tall bone greataxe with twelve chunky, faceted vertebrae following the original gentle S-curve,
a bearded scapula blade and a hooked bone poll.
The handle was rebuilt on 2026-09-30 with broad rear crests, recessed bone joints,
an angular heel and a flared head seat. The approved head geometry is retained exactly.
Shorter bone processes at three grip seats accommodate the player's low grip and the Skeleton's two hand positions. Grounded pivot;
Blender Z is up, the blade extends along +X. Dimensions are baked in metres.

Reference inspected locally: `Characters/Skeleton/model/Texture/Skeleton_d.tga`
(128 x 128), its importer (point filtering), and `model/Materials/Skeleton.mat`
(white tint, metallic 0, glossiness 0.24, albedo plus normal texture).
The authored 256 x 256 atlas follows the reference's warm tan/cream bone,
painted brown recesses and broad shading. It contains no copied game texture.
The axe uses geometry and painted shading rather than a fine bump texture.

Build from the workspace root:

```powershell
& "$env:USERPROFILE/tools/blender/blender.exe" --background --factory-startup --python-exit-code 1 --python AssetWorkshop/assets/ecp_bone_greataxe/build.py
```

`haft.py` builds the replacement handle using `ecp_spine_greataxe/axe_mesh.py`
and the workshop's bone paint recipes. The comparison also needs the local ValheimReference export.
The approved head is preserved with its original UVs in `approved_head.blend`.
`original_held_source.blend` preserves the original finished scene.

Run `held_showcase.py` with Blender after building to make the original Valheim
Skeleton hold the axe and play a five-second idle loop (24 fps, frames 1–120;
frame 121 matches frame 1). Hand IK targets follow the axe and feet stay planted.
`check_idle.py` checks the seam, hand reach, foot drift and actual movement.
Open `out/skeleton_holding_axe.blend` with `--python open_idle.py` to start
textured playback automatically. Space pauses/resumes the animation.

Outputs in `out/`: editable packed-texture `.blend`, `.fbx`, albedo PNG,
dimensions/triangle manifest, four-view preview, and `skeleton_comparison.png`
with the actual game Skeleton under the same Blender lighting. The comparison
blend is local reference only; the exported axe contains only the new geometry.

The remake is 2,916 triangles (1,632 retained head, 1,284 new handle), down from
13,222. `check_model.py` verifies the height and approved head vertex positions;
`check_idle.py` passes with less than 0.1 mm hand target error. `BRIEF.md` records
the style checks and retained atlas/scale exceptions. The original built model
and front preview are backed up locally in `out/before_remake/`.

For game integration, use point filtering, metallic 0, smoothness 0.24 and the
game Skeleton shader/material dressed with the new albedo. This delivery is
the model and texture. Installed into Elite Creatures Pack (ecp_headsman bundle) on 2026-09-30,
unreleased, not yet checked inside Valheim.
