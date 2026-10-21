"""Lineups: a built asset in one frame beside the game's own prefabs, all point filtered as the game samples textures,
under one sun and sky on dark earth, from the front, three-quarters and close up. The check that it looks like it
belongs. Entry point: blender/lineup.py (it finds the references in the codex); by hand:

    entries = [lineup.asset_entry(blend_path, name, item=True)] + [lineup.reference_entry(p) for p in prefabs]
    lineup.run(entries, out_dir)

Items stand up for the camera. The game's item prefabs lie flat (as dropped), so an item lying flat is stood on end:
its longest side vertical, its upper face (a shield's front) towards the camera, its grip (the prefab's `attach`
point, or the asset's origin) at the bottom. A standing item turns its broad side to the camera. Everything else
(pieces, creatures, rocks) stands as built. Game objects come from the local reference export, tagged, never exported.
"""
import math
import os

import bpy
from mathutils import Matrix, Vector

from . import prefab

SKY, SKY_STRENGTH = (0.42, 0.46, 0.44), 0.9
SUN_ENERGY, SUN_ROTATION = 2.4, (math.radians(50), 0.0, math.radians(25))
EARTH = (0.075, 0.06, 0.045)
RESOLUTION = (1600, 1000)
VIEWS = {"front": None, "turn": Vector((0.55, -1.0, 0.45)), "close": Vector((0.35, -1.0, 0.3)),
         "back": Vector((-0.55, 1.0, 0.45))}
ITEM_FAMILIES = ("weapon", "tool", "shield", "armour", "item")


def asset_entry(blend, name, item):
    """Opens the asset's built .blend (replacing the scene) and keeps only its visual mesh."""
    bpy.ops.wm.open_mainfile(filepath=blend)
    for obj in [o for o in bpy.data.objects if o.name != name]:
        bpy.data.objects.remove(obj, do_unlink=True)
    obj = bpy.data.objects[name]
    return {"root": obj, "meshes": [obj], "label": name, "grip": Vector(), "item": item}


def reference_entry(path):
    """Loads a game prefab; an item's grip is its `attach` node."""
    name = os.path.splitext(os.path.basename(path))[0]
    root = prefab.load(path, "game " + name)
    grip = _attach(path)
    return {"root": root, "meshes": prefab.meshes(root), "label": name, "grip": grip, "item": grip is not None}


def run(entries, out, views=("front", "turn", "close")):
    """Stands the items up, sets them in a row, stages, renders each view to out/<view>.png, saves out/lineup.blend."""
    entries = [e for e in entries if e["meshes"]]
    for entry in entries:
        orient(entry)
    low, high = layout(entries)
    point_filter()
    stage(max(high.x - low.x, 10.0))
    labels(entries, high.z - low.z)
    os.makedirs(out, exist_ok=True)
    for view in views:
        shot(view, _framed(view, entries), os.path.join(out, view + ".png"))
    bpy.context.scene.camera = bpy.data.objects.get("lineup_turn") or bpy.context.scene.camera
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, "lineup.blend"))


def orient(entry):
    """Stands a lying item up, or turns a standing one's broad side to the camera (see the module's notes)."""
    if not entry["item"]:
        return
    low, high = bounds(entry["meshes"])
    size = high - low
    if size.z <= min(size.x, size.y):
        turn = stand_up(size, entry["grip"], (low + high) / 2)
    elif size.x < size.y:
        turn = Matrix.Rotation(math.radians(90), 3, 'Z')
    else:
        return
    entry["root"].matrix_world = turn.to_4x4() @ entry["root"].matrix_world


def stand_up(size, grip, centre):
    """The rotation taking a flat-lying item's longest side to +Z and its upper face (+Z) to -Y, grip end down."""
    long_axis = Vector((1, 0, 0)) if size.x >= size.y else Vector((0, 1, 0))
    up = Vector((0, 0, 1))
    source = Matrix((long_axis, up, long_axis.cross(up))).transposed()
    target = Matrix((Vector((0, 0, 1)), Vector((0, -1, 0)), Vector((1, 0, 0)))).transposed()
    turn = target @ source.transposed()
    if grip is not None and (turn @ grip).z - (turn @ centre).z > 0.15 * max(size.x, size.y):
        turn = Matrix.Rotation(math.pi, 3, 'Y') @ turn
    return turn


def layout(entries):
    """Sets the entries left to right on z = 0, centred on y = 0, a gap apart; returns the row's bounds."""
    boxes = [bounds(e["meshes"]) for e in entries]
    heights = sorted(high.z - low.z for low, high in boxes)
    gap, x = max(0.2, 0.3 * heights[len(heights) // 2]), 0.0
    for entry, (low, high) in zip(entries, boxes):
        entry["root"].location += Vector((x - low.x, -(low.y + high.y) / 2, -low.z))
        x += high.x - low.x + gap
    return bounds([m for e in entries for m in e["meshes"]])


def bounds(meshes):
    return prefab.bounds(list(meshes))


def point_filter():
    """Every image texture sampled nearest-texel, as the game's point-filtered textures are."""
    for mat in bpy.data.materials:
        for node in (mat.node_tree.nodes if mat.node_tree else []):
            if node.bl_idname == 'ShaderNodeTexImage':
                node.interpolation = 'Closest'


def stage(floor_size=10.0):
    """EEVEE, the lineup's sky and sun, and a dark earth floor on z = 0."""
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 32
    s.render.resolution_x, s.render.resolution_y = RESOLUTION
    s.render.resolution_percentage = 100
    s.view_settings.view_transform = 'Standard'
    s.world = bpy.data.worlds.new('lineup')
    background = next(n for n in s.world.node_tree.nodes if n.bl_idname == 'ShaderNodeBackground')
    background.inputs['Color'].default_value = (*SKY, 1.0)
    background.inputs['Strength'].default_value = SKY_STRENGTH
    sun = bpy.data.objects.new('lineup_sun', bpy.data.lights.new('lineup_sun', 'SUN'))
    sun.data.energy, sun.rotation_euler = SUN_ENERGY, SUN_ROTATION
    s.collection.objects.link(sun)
    bpy.ops.mesh.primitive_plane_add(size=max(400.0, floor_size * 20))
    floor = bpy.context.active_object
    floor.name = 'lineup_floor'
    floor.data.materials.append(flat('lineup_earth', EARTH, roughness=1.0))


def flat(name, rgb, roughness=0.8, emit=0.0):
    mat = bpy.data.materials.new(name)
    bsdf = next(n for n in mat.node_tree.nodes if n.bl_idname == 'ShaderNodeBsdfPrincipled')
    bsdf.inputs['Base Color'].default_value = (*rgb, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    if emit:
        bsdf.inputs['Emission Color'].default_value = (*rgb, 1.0)
        bsdf.inputs['Emission Strength'].default_value = emit
    return mat


def labels(entries, height):
    """Each entry's name above its back edge, facing the camera."""
    size = max(0.04, 0.06 * height)
    mat = flat('lineup_label', (0.9, 0.88, 0.8), emit=0.6)
    for entry in entries:
        low, high = bounds(entry["meshes"])
        label(entry["label"], Vector(((low.x + high.x) / 2, high.y, high.z + size * 0.6)), size, mat)


def label(text, location, size, mat):
    curve = bpy.data.curves.new('label_' + text, 'FONT')
    curve.body, curve.size, curve.align_x = text, size, 'CENTER'
    obj = bpy.data.objects.new('label_' + text, curve)
    obj.location, obj.rotation_euler = location, (math.radians(90), 0.0, 0.0)
    obj.visible_shadow = False
    obj.data.materials.append(mat)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def shot(view, framed, path, lens=None):
    """Renders one view of the framed objects to path; the camera stays in the scene as lineup_<view>."""
    s = bpy.context.scene
    camera = bpy.data.objects.new('lineup_' + view, bpy.data.cameras.new('lineup_' + view))
    s.collection.objects.link(camera)
    bpy.context.view_layer.update()
    low, high = _bounds_of(framed)
    if VIEWS.get(view) is None:
        _front(camera, low, high)
    else:
        _perspective(camera, low, high, VIEWS[view].normalized(), lens or (35 if view == "turn" else 50))
    s.camera = camera
    s.render.filepath = path
    bpy.ops.render.render(write_still=True)
    return path


def _framed(view, entries):
    """What a view frames: the whole row, or for the close-up the asset and its first neighbour."""
    chosen = entries[:2] if view == "close" else entries
    return [m for e in chosen for m in e["meshes"]] + _label_objects(chosen if view != "close" else [])


def _label_objects(entries):
    names = {'label_' + e["label"] for e in entries}
    return [o for o in bpy.data.objects if o.name in names]


def _bounds_of(objects):
    corners = [o.matrix_world @ Vector(c) for o in objects for c in o.bound_box]
    low = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
    high = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
    return low, high


def _front(camera, low, high):
    """Orthographic, level, looking along +Y, fitting the bounds with a small margin."""
    centre, size = (low + high) / 2, high - low
    aspect = RESOLUTION[0] / RESOLUTION[1]
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = max(size.x, size.z * aspect) * 1.08
    camera.location = Vector((centre.x, low.y - 20.0, centre.z))
    camera.rotation_euler = Vector((0, 1, 0)).to_track_quat('-Z', 'Y').to_euler()
    camera.data.clip_end = 200.0


def _perspective(camera, low, high, direction, lens):
    """A perspective camera looking at the bounds' centre from `direction`, as close as keeps every corner in frame."""
    camera.data.type, camera.data.lens = 'PERSP', lens
    centre = (low + high) / 2
    rotation = (-direction).to_track_quat('-Z', 'Y')
    right, up = rotation @ Vector((1, 0, 0)), rotation @ Vector((0, 1, 0))
    half_w = camera.data.sensor_width / 2 / lens
    half_h = half_w * RESOLUTION[1] / RESOLUTION[0]
    corners = [Vector((x, y, z)) - centre for x in (low.x, high.x) for y in (low.y, high.y) for z in (low.z, high.z)]
    distance = max(c.dot(direction) + max(abs(c.dot(right)) / half_w, abs(c.dot(up)) / half_h) for c in corners)
    camera.location = centre + direction * distance * 1.06
    camera.rotation_euler = rotation.to_euler()
    camera.data.clip_start, camera.data.clip_end = 0.01, distance * 10 + 50


def _attach(path):
    """The prefab's `attach` point relative to its root (Blender axes, the root's scale kept), or None."""
    tree = prefab._Prefab(path)        # the loader's own reading of the prefab file
    for transform, (cls, _) in tree.docs.items():
        if cls in prefab.TRANSFORMS and tree.is_local(transform) and tree.name(transform) == "attach":
            return tree.world(transform).to_translation()
    return None


def is_item_category(key):
    return bool(key) and key.split(".")[0] in ITEM_FAMILIES

