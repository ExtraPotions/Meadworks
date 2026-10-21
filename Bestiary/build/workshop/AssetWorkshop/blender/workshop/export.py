"""Writes what Unity imports: the FBX (Unity axes, metres), the baked PNGs and the manifest the bundle build reads."""
import json

import bpy

from . import scene


def fbx(path, objs):
    """-Z forward, Y up with the transform baked in: parts arrive unrotated and the model's front (-Y) faces +Z."""
    scene.select_only(objs)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=True,
        add_leaf_bones=False, bake_anim=False, path_mode='STRIP', embed_textures=False)


def png(image, path):
    image.filepath_raw = path
    image.file_format = 'PNG'
    image.save()
    image.filepath = path


def manifest(path, data):
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(data, handle, indent=2)
        handle.write("\n")


def unity_size(low, high):
    """Blender (x, y, z) extents as Unity (width x, height y, depth z), in metres."""
    size = high - low
    return [round(size.x, 3), round(size.z, 3), round(size.y, 3)]
