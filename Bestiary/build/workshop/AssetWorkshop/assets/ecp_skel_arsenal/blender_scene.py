"""The skeleton arsenal showcase in Blender, from the Unity bake (unity/Assets/Editor/SkelArsenal/ArsenalBake):

    blender --background --factory-startup --python assets/ecp_skel_arsenal/blender_scene.py -- [--bake <folder>]

Reads <bake>/arsenal.json (default assets/ecp_skel_arsenal/out/blender) and each skeleton's <weapon>/scene.json, and
builds one object per baked renderer - the game's Skeleton (reference, preview only), its weapon, the bow's string and
arrow - on its texture, played by a Mesh Cache modifier. Each skeleton loops its own bake (idle, attack, idle) on and
on: the cache's frame is keyed as a sawtooth with a Cycles modifier, no scripts. The timeline cuts between cameras:
the whole row, then each weapon close up while its skeleton attacks, then the spine the skeletons drop, turning on
the ground. Labels name each weapon. Saves <bake>/skeleton_arsenal.blend; blender_play.py opens it playing.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
from workshop import materials, scene  # noqa: E402

LINEUP_SECONDS, CLOSE_SECONDS = 10.0, 5.0
LONG = ("Spear", "Atgeir")                      # close-ups stand further back for the long weapons
FONT = os.path.join(HERE, '..', '..', 'unity', 'Assets', 'Reference', 'Norsebold.otf')
SPINE = os.path.join(HERE, '..', 'ecp_spine', 'out', 'ecp_spine.blend')
HERO = Vector((0.0, -2.6, 0.0))                  # the spine on show, in front of the row


def main():
    folder = _argument('--bake', os.path.join(HERE, 'out', 'blender'))
    lineup = json.load(open(os.path.join(folder, 'arsenal.json'), encoding='utf-8'))
    scene.clear()
    fps = lineup['fps']
    slots = _slots(lineup, fps)
    for group in lineup['groups']:
        _group(group, folder, slots[group['label']])
    _stage()
    _drops(lineup['groups'])
    _cameras(lineup['groups'], slots)
    _timeline(fps, slots)
    _views()
    path = os.path.join(folder, 'skeleton_arsenal.blend')
    # Keep textures with the scene; caches travel in the sibling weapon folders.
    bpy.ops.file.pack_all()
    for obj in bpy.context.scene.objects:
        for modifier in obj.modifiers:
            if modifier.type == 'MESH_CACHE':
                modifier.filepath = '//' + os.path.relpath(modifier.filepath, folder).replace(os.sep, '/')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP blend {path}: {len(lineup['groups'])} skeletons, {bpy.context.scene.frame_end} frames")


def _argument(name, default):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv else default


def _slots(lineup, fps):
    """The first frame of each shot: the row, each weapon in turn, the spine; and 'end' after the last."""
    slots, frame = {'Lineup': 1}, 1 + round(LINEUP_SECONDS * fps)
    for label in [g['label'] for g in lineup['groups']] + ['Spine']:
        slots[label] = frame
        frame += round(CLOSE_SECONDS * fps)
    slots['end'] = frame
    return slots


def _group(group, folder, start):
    """One skeleton's baked parts, looping their bake from `start` (its close-up) on and back."""
    sub = os.path.join(folder, group['folder'])
    data = json.load(open(os.path.join(sub, 'scene.json'), encoding='utf-8'))
    collection = bpy.data.collections.new(group['label'])
    bpy.context.scene.collection.children.link(collection)
    for part in data['parts']:
        obj = _part(part, sub, collection)
        _loop(obj, data['frames'], start)
    _label(group['label'], Vector((group['x'], 0.0, 2.25)), collection, size=0.28)


def _part(part, folder, collection):
    """One baked renderer: its mesh, UVs and texture, played by its point cache."""
    v, t = part['vertices'], part['triangles']
    mesh = bpy.data.meshes.new(part['name'])
    mesh.from_pydata([v[i:i + 3] for i in range(0, len(v), 3)], [], [t[i:i + 3] for i in range(0, len(t), 3)])
    uvs = part['uvs']
    if uvs:
        layer = mesh.uv_layers.new(name='uv')
        for loop in mesh.loops:
            layer.data[loop.index].uv = (uvs[2 * loop.vertex_index], uvs[2 * loop.vertex_index + 1])
    mesh.materials.append(_material(part))
    obj = bpy.data.objects.new(part['name'], mesh)
    collection.objects.link(obj)
    scene.select_only([obj])
    bpy.ops.object.shade_smooth_by_angle(angle=0.6)
    cache = obj.modifiers.new('bake', 'MESH_CACHE')
    cache.cache_format = 'PC2'
    cache.filepath = os.path.join(folder, part['cache'])
    cache.play_mode = 'CUSTOM'
    cache.time_mode = 'FRAME'
    return obj


def _loop(obj, frames, start):
    """The cache frame as a sawtooth: 0 at `start`, frames - 1 a bake later, repeating both ways."""
    cache = obj.modifiers['bake']
    path = 'modifiers["bake"].eval_frame'
    for frame, value in ((start, 0.0), (start + frames, float(frames))):
        cache.eval_frame = value
        obj.keyframe_insert(data_path=path, frame=frame)
    for curve in _fcurves(obj):
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
        curve.modifiers.new('CYCLES')


def _fcurves(obj):
    action = obj.animation_data.action
    if hasattr(action, 'fcurves'):
        return list(action.fcurves)
    from bpy_extras import anim_utils
    return list(anim_utils.action_get_channelbag_for_slot(action, obj.animation_data.action_slot).fcurves)


def _material(part):
    texture = part['texture']
    name = os.path.splitext(os.path.basename(texture))[0] if texture else part['name']
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = materials.flat(name, tuple(part['color']), roughness=0.85)
    bsdf = materials.principled(mat)
    if part['glow']:
        bsdf.inputs['Emission Color'].default_value = (*part['color'], 1.0)
        bsdf.inputs['Emission Strength'].default_value = 4.0
    if texture and os.path.exists(texture):
        image = mat.node_tree.nodes.new('ShaderNodeTexImage')
        image.image = bpy.data.images.load(texture, check_existing=True)
        image.interpolation = 'Closest'   # the game's textures are point filtered
        mat.node_tree.links.new(image.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


def _label(text, location, collection, size=0.34):
    curve = bpy.data.curves.new('label_' + text, 'FONT')
    curve.body = text
    curve.align_x = 'CENTER'
    curve.size = size
    curve.extrude = 0.012
    if os.path.exists(FONT):
        curve.font = bpy.data.fonts.load(FONT, check_existing=True)
    label = bpy.data.objects.new('label_' + text, curve)
    label.location = location
    label.rotation_euler = (math.radians(90), 0.0, 0.0)   # standing, read from -Y
    curve.materials.append(_ink())
    collection.objects.link(label)
    return label


def _ink():
    if 'label_ink' in bpy.data.materials:
        return bpy.data.materials['label_ink']
    mat = materials.flat('label_ink', (0.85, 0.72, 0.45), roughness=0.6)
    bsdf = materials.principled(mat)
    bsdf.inputs['Emission Color'].default_value = (0.85, 0.72, 0.45, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 0.6
    return mat


def _stage():
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = 'ground'
    ground.data.materials.append(materials.flat('ground', (0.11, 0.12, 0.075), roughness=0.95))
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (0.8, 0.15, -0.5)
    bpy.context.scene.collection.objects.link(sun)
    world = bpy.data.worlds.new('sky')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.33, 0.40, 0.46, 1.0)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.9
    bpy.context.scene.world = world


def _drops(groups):
    """The spine item (ecp_spine) as the skeletons drop it: one turning in front of the row, a few in the grass."""
    with bpy.data.libraries.load(SPINE) as (source, target):
        target.objects = [name for name in source.objects if name == 'ecp_spine']
    item = target.objects[0]
    collection = bpy.data.collections.new('Spine drops')
    bpy.context.scene.collection.children.link(collection)
    hero = _drop(item, collection, HERO, 0.0)
    for i, group in enumerate(groups[::2]):
        _drop(item, collection, Vector((group['x'] + 0.55, -1.0 - 0.2 * (i % 2), 0.0)), 40.0 + 70.0 * i)
    for frame, turn in ((1, 0.0), (241, 2 * math.pi)):
        hero.rotation_euler.z = turn
        hero.keyframe_insert('rotation_euler', index=2, frame=frame)
    for curve in _fcurves(hero):
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
        curve.modifiers.new('CYCLES')
    _label('Spine', HERO + Vector((0.0, 0.3, 0.55)), collection, size=0.12)


def _drop(item, collection, location, yaw):
    obj = item.copy()
    collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = (0.0, 0.0, math.radians(yaw))
    for slot in obj.material_slots:
        for node in slot.material.node_tree.nodes if slot.material else []:
            if node.bl_idname == 'ShaderNodeTexImage':
                node.interpolation = 'Closest'
    return obj


def _cameras(groups, slots):
    marker = bpy.context.scene.timeline_markers
    row = _tracking(groups, slots)
    marker.new('Lineup', frame=slots['Lineup']).camera = row
    for group in groups:
        x, far = group['x'], group['label'] in LONG
        eye = Vector((x + 3.0, -4.8, 1.7)) if far else Vector((x + 1.9, -3.2, 1.55))
        cam = _camera('cam_' + group['label'].lower(), eye, Vector((x, 0.1 if far else 0.0, 1.15)), 30 if far else 35)
        marker.new(group['label'], frame=slots[group['label']]).camera = cam
    cam = _camera('cam_spine', HERO + Vector((0.9, -1.35, 0.8)), HERO + Vector((0.0, 0.0, 0.15)), 50)
    marker.new('Spine', frame=slots['Spine']).camera = cam
    bpy.context.scene.camera = row


def _tracking(groups, slots):
    """The row shot: the camera tracks slowly along the row from the first skeleton to the last, a few in view at once."""
    first, last = groups[0]['x'], groups[-1]['x']
    cam = _camera('cam_lineup', Vector((first - 1.5, -5.6, 1.55)), Vector((first + 1.0, 0.0, 1.05)), 26)
    for frame, x in ((slots['Lineup'], first - 1.5), (slots[groups[0]['label']] - 1, last - 1.0)):
        cam.location.x = x
        cam.keyframe_insert('location', index=0, frame=frame)
    for curve in _fcurves(cam):
        for key in curve.keyframe_points:
            key.interpolation = 'LINEAR'
    return cam


def _camera(name, eye, target, lens):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    cam.data.lens = lens
    cam.data.clip_start = 0.02
    cam.location = eye
    cam.rotation_euler = (target - eye).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.scene.collection.objects.link(cam)
    return cam


def _timeline(fps, slots):
    s = bpy.context.scene
    s.render.fps = int(round(fps))
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    s.frame_start, s.frame_end, s.frame_current = 1, slots['end'] - 1, 1


def _views():
    """Every 3D view in material preview, looking through the camera."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.shading.type = 'MATERIAL'
                    space.shading.use_scene_lights = True
                    space.shading.use_scene_world = True
                    space.overlay.show_extras = False
                    space.overlay.show_floor = False
                    if space.region_3d is not None:
                        space.region_3d.view_perspective = 'CAMERA'


if __name__ == '__main__':
    main()
