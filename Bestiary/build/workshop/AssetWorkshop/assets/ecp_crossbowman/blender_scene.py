"""The Skeleton Crossbowman's animations in Blender, from the Unity bake (unity/Assets/Editor/Crossbow/XbowBlenderBake):

    blender --background --factory-startup --python assets/ecp_crossbowman/blender_scene.py -- [--bake <folder>] [--compare]

Reads <bake>/scene.json (default assets/ecp_crossbowman/out/blender) and builds one object per baked renderer - the
game's Skeleton (reference, preview only), the crossbow, its string, the quiver, the bolts - each with its texture and
a Mesh Cache modifier playing its .pc2 point cache, so the timeline plays exactly what the game's Animator played in
Unity. Markers on the timeline name each part: the carry idle, raise and aim, the shot, the reload, the walk, the run.
Adds a ground, a sun, a camera like the preview video's, and saves <bake>/crossbowman_animations.blend with the 3D
views in material preview through the camera. Nothing here goes into a bundle. With --compare it also puts a second
skeleton beside the first doing the same animation with the Gravebranch crossbow (blender_compare.py) and saves
<bake>/crossbowman_compare.blend.
"""
import json
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
from workshop import materials, scene  # noqa: E402

CAMERA = (Vector((-3.4, -4.3, 1.75)), Vector((0.0, -0.4, 1.05)))


def main():
    folder = _argument('--bake', os.path.join(HERE, 'out', 'blender'))
    data = json.load(open(os.path.join(folder, 'scene.json'), encoding='utf-8'))
    scene.clear()
    for part in data['parts']:
        _part(part, folder)
    _stage()
    _timeline(data)
    compare = '--compare' in sys.argv
    if compare:
        sys.path.insert(0, HERE)
        import blender_compare
        blender_compare.build(folder)
        blender_compare.camera()
    _views()
    path = os.path.join(folder, 'crossbowman_compare.blend' if compare else 'crossbowman_animations.blend')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP blend {path}: {len(data['parts'])} parts, {data['frames']} frames")


def _argument(name, default):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv else default


def _part(part, folder):
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
    bpy.context.collection.objects.link(obj)
    scene.select_only([obj])
    bpy.ops.object.shade_smooth_by_angle(angle=0.6)
    cache = obj.modifiers.new('bake', 'MESH_CACHE')
    cache.cache_format = 'PC2'
    cache.filepath = os.path.join(folder, part['cache'])
    cache.frame_start = 1.0
    return obj


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


def _stage():
    bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = 'ground'
    ground.data.materials.append(materials.flat('ground', (0.13, 0.16, 0.10), roughness=0.95))
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (0.75, 0.2, -0.6)
    bpy.context.collection.objects.link(sun)
    world = bpy.data.worlds.new('sky')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.36, 0.44, 0.48, 1.0)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.9
    bpy.context.scene.world = world
    camera = bpy.data.objects.new('camera', bpy.data.cameras.new('camera'))
    camera.data.lens = 45
    camera.location = CAMERA[0]
    camera.rotation_euler = (CAMERA[1] - CAMERA[0]).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera


def _timeline(data):
    s = bpy.context.scene
    s.render.fps = int(round(data['fps']))
    s.frame_start, s.frame_end, s.frame_current = 1, data['frames'], 1
    for name, frame in zip(data['markerNames'], data['markerFrames']):
        s.timeline_markers.new(name, frame=frame + 1)


def _views():
    """Every 3D view in material preview, looking through the camera; the timeline shows the markers' names."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.shading.type = 'MATERIAL'
                    space.overlay.show_floor = False
                    if space.region_3d is not None:
                        space.region_3d.view_perspective = 'CAMERA'


main()
