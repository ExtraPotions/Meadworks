"""A Blender scene of a route (b) playback: our body on the game creature, played frame by frame as Unity's Animator
played it through the game's own controller (GameRigPreview records it with the crossbowman's XbowCache: each
renderer's mesh, texture and a .pc2 point cache of its vertices, in Blender's axes).

    blender --background --factory-startup --python blender/workshop/gamerig_scene.py --
            --bake <folder> [--name <name>] [--render]

Builds one object per baked renderer with its texture (point filtered, as the game draws it) and a Mesh Cache modifier,
markers naming each phase of the playback (wakeup, idle, walk, run, the attacks, stagger), a ground with a 1 m grid,
a sun and a camera, and saves <folder>/<name>_animations.blend with the 3D views in material preview through the camera.
--render also writes a still of the middle frame of every phase into <folder>/stills.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
from workshop import materials, scene  # noqa: E402


def main():
    """Reads the bake, builds and saves the scene, and renders its stills when asked."""
    folder = os.path.abspath(_argument("--bake"))
    name = _argument("--name", "gamerig")
    data = json.load(open(os.path.join(folder, "scene.json"), encoding="utf-8"))
    scene.clear()
    parts = [_part(part, folder) for part in data["parts"]]
    _stage(parts)
    _timeline(data)
    _views()
    path = os.path.join(folder, f"{name}_animations.blend")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP blend {path}: {len(parts)} parts, {data['frames']} frames", flush=True)
    if "--render" in sys.argv:
        _stills(data, os.path.join(folder, "stills"))


def _argument(key, default=None):
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if key in argv:
        return argv[argv.index(key) + 1]
    if default is None:
        raise SystemExit(f"gamerig_scene.py: {key} is required")
    return default


def _part(part, folder):
    """One baked renderer: its mesh at the first frame it shows, UVs and texture, played by its point cache."""
    v, t, uvs = part["vertices"], part["triangles"], part["uvs"]
    mesh = bpy.data.meshes.new(part["name"])
    mesh.from_pydata([v[i:i + 3] for i in range(0, len(v), 3)], [], [t[i:i + 3] for i in range(0, len(t), 3)])
    if uvs:
        layer = mesh.uv_layers.new(name="uv")
        for loop in mesh.loops:
            layer.data[loop.index].uv = (uvs[2 * loop.vertex_index], uvs[2 * loop.vertex_index + 1])
    mesh.materials.append(_material(part))
    obj = bpy.data.objects.new(part["name"], mesh)
    bpy.context.collection.objects.link(obj)
    scene.select_only([obj])
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(60))
    cache = obj.modifiers.new("playback", 'MESH_CACHE')
    cache.cache_format, cache.filepath, cache.frame_start = 'PC2', os.path.join(folder, part["cache"]), 1.0
    return obj


def _material(part):
    mat = materials.flat(part["name"] + "_look", tuple(part["color"]), roughness=0.85)
    texture = part["texture"]
    if texture and os.path.exists(texture):
        image = mat.node_tree.nodes.new('ShaderNodeTexImage')
        image.image = bpy.data.images.load(texture, check_existing=True)
        image.interpolation = 'Closest'
        mat.node_tree.links.new(image.outputs['Color'], materials.principled(mat).inputs['Base Color'])
    return mat


def _stage(parts):
    """A mossy ground with a 1 m grid, a low sun, a sky, a camera on the creature's front left."""
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=16, y_subdivisions=16, size=16)
    ground = bpy.context.active_object
    ground.name = "ground"
    ground.data.materials.append(materials.flat("ground", (0.12, 0.15, 0.08), roughness=0.95))
    ground.modifiers.new("lines", 'WIREFRAME').thickness = 0.015
    floor = bpy.data.objects.new("floor", ground.data.copy())
    floor.data.materials[0] = materials.flat("floor", (0.18, 0.22, 0.12), roughness=0.95)
    floor.location.z = -0.002
    bpy.context.collection.objects.link(floor)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", 'SUN'))
    sun.data.energy, sun.rotation_euler = 3.5, (0.8, 0.15, 2.6)
    bpy.context.collection.objects.link(sun)
    world = bpy.data.worlds.new("sky")
    bpy.context.scene.world = world
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
    background.inputs[0].default_value, background.inputs[1].default_value = (0.4, 0.48, 0.52, 1.0), 0.9
    _camera(parts)


def _camera(parts):
    low, high = scene.bounds(parts)
    target = Vector(((low.x + high.x) / 2, (low.y + high.y) / 2, 1.0))
    camera = bpy.data.objects.new("camera", bpy.data.cameras.new("camera"))
    camera.data.lens = 45
    camera.location = target + Vector((1.9, -3.9, 0.55))
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(camera)
    bpy.context.scene.camera = camera


def _timeline(data):
    s = bpy.context.scene
    s.render.fps = int(round(data["fps"]))
    s.frame_start, s.frame_end, s.frame_current = 1, data["frames"], 1
    for label, frame in zip(data["markerNames"], data["markerFrames"]):
        s.timeline_markers.new(label, frame=frame + 1)


def _views():
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.shading.type = 'MATERIAL'
                    if space.region_3d is not None:
                        space.region_3d.view_perspective = 'CAMERA'


def _stills(data, folder):
    """The middle frame of every phase, rendered with Eevee."""
    os.makedirs(folder, exist_ok=True)
    s = bpy.context.scene
    s.render.engine, s.render.resolution_x, s.render.resolution_y = 'BLENDER_EEVEE', 640, 480
    frames = data["markerFrames"] + [data["frames"]]
    for i, label in enumerate(data["markerNames"]):
        s.frame_set((frames[i] + frames[i + 1]) // 2 + 1)
        s.render.filepath = os.path.join(folder, f"{i:02d}_{label.replace(' ', '_').replace('/', '_')}.png")
        bpy.ops.render.render(write_still=True)
    print(f"WORKSHOP blender stills: {len(data['markerNames'])} in {folder}", flush=True)


if __name__ == "__main__":
    main()
