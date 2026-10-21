"""Exports the mimic for Unity: the rig, our parts (never the game's chest) and one animation per clip.

    blender --background --factory-startup --python-exit-code 1 --python assets/crypt_mimic/export_unity.py

Writes out/unity/crypt_mimic.fbx and crypt_mimic.json: the clips (frames, looping, the game's state tag, the frames
where a bite lands) and the part materials as sRGB colours.

The clips carry no travel: the lunge's leap is played on the spot and the mod moves the creature (EliteCreaturesPack
MimicLeap, following LUNGE's forward curve in clips.py). Unity's root motion from a Blender armature came out
backwards or downwards however the rig was turned: Blender's exporter gives the armature's contents and its motion
opposite forwards, and Unity reads root motion relative to the moving node's own tilted frame.
"""
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.dont_write_bytecode = True
sys.path[:0] = [HERE, os.path.join(HERE, "..", "..", "blender")]

import bpy  # noqa: E402
from mathutils import Matrix  # noqa: E402

import parts  # noqa: E402
import rig as mimic_rig  # noqa: E402
from clips import CLIPS  # noqa: E402
from poses import key  # noqa: E402
from workshop import scene  # noqa: E402

NAME, FPS = "crypt_mimic", 30
OUT = os.path.join(HERE, "out", "unity")


def actions(rig):
    rig.animation_data_create()
    for name, (keys, *_rest) in CLIPS.items():
        action = bpy.data.actions.new(name)
        action.use_fake_user = True
        rig.animation_data.action = action
        for frame, values in keys:
            key(rig, frame, dict(values, forward=0.0, side=0.0))   # travel is the mod's: see the docstring
    rig.animation_data.action = None
    for bone in rig.pose.bones:
        bone.location, bone.scale = (0, 0, 0), (1, 1, 1)
        bone.rotation_euler = (0, 0, 0)


def turn_around(rig):
    """
    Unity maps Blender's +Y to its +Z, so a rig facing -Y would face -Z. The half turn is baked into the rig itself -
    every bone's rest position and roll, and the parts with them - rather than put on the armature object, whose
    rotation Unity leaves out of root motion. After the turn the root bone (flat, pointing forward) has exactly the
    rig's own frame, so its travel converts to Unity the way the rest pose does. Must run before the clips are keyed
    (their keys are relative to the rest pose). Unity's RootMotionCheck fails the build if a lunge goes backwards.
    """
    turn = Matrix.Rotation(math.pi, 4, 'Z')
    children = [o for o in bpy.data.objects if o.parent == rig]
    worlds = {o.name: o.matrix_world.copy() for o in children}
    scene.select_only([rig])
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in rig.data.edit_bones:
        bone.transform(turn)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()
    for obj in children:
        if obj.parent_type == 'BONE':
            obj.matrix_world = turn @ worlds[obj.name]   # rigid parts ride their (turned) bones
        else:
            obj.data.transform(turn)                     # the skinned tongue: its vertices, bound in armature space
    bpy.context.view_layer.update()


def export_fbx(rig, path):
    scene.select_only([rig] + [o for o in scene.meshes()], active=rig)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
        armature_nodetype='NULL', use_armature_deform_only=False, mesh_smooth_type='FACE',
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True, bake_anim_force_startend_keying=True, bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0, path_mode='STRIP')


def manifest():
    clips = [{"name": name, "last": keys[-1][0], "loop": loops, "tag": tag, "events": list(events)}
             for name, (keys, loops, tag, events) in CLIPS.items()]
    return {"asset": NAME, "fbx": NAME + ".fbx", "rig": "mimic_rig", "rootBone": "", "fps": FPS,
            "clips": clips, "materials": [_material(m) for m in bpy.data.materials if m.name.startswith("mimic_")]}


def _material(mat):
    """Colour, smoothness and glow; for a material on the game's textures, which ones (paths in the reference export),
    so Unity and the mod use the game's own texture rather than a copy of it."""
    from workshop import materials
    bsdf = materials.principled(mat)
    textured = "reference_albedo" in mat
    colour = [1.0, 1.0, 1.0] if textured else [_srgb(c) for c in bsdf.inputs['Base Color'].default_value[:3]]
    glow = bsdf.inputs['Emission Strength'].default_value
    emission = [_srgb(c) for c in bsdf.inputs['Emission Color'].default_value[:3]] if glow > 0 else [0, 0, 0]
    return {"name": mat.name, "color": colour, "smoothness": round(1 - bsdf.inputs['Roughness'].default_value, 3),
            "emission": emission, "emissionStrength": glow,
            "albedo": mat.get("reference_albedo", ""), "normal": mat.get("reference_normal", "")}


def _srgb(linear):
    value = linear * 12.92 if linear <= 0.0031308 else 1.055 * linear ** (1 / 2.4) - 0.055
    return round(min(max(value, 0.0), 1.0), 4)


def main():
    os.makedirs(OUT, exist_ok=True)
    scene.clear()
    bpy.context.scene.render.fps = FPS
    rig = mimic_rig.armature()
    parts.build(rig)
    turn_around(rig)
    actions(rig)
    export_fbx(rig, os.path.join(OUT, NAME + ".fbx"))
    with open(os.path.join(OUT, NAME + ".json"), "w", encoding="utf-8") as handle:
        json.dump(manifest(), handle, indent=2)
    print("WORKSHOP exported", len(CLIPS), "clips to", OUT, flush=True)


main()
