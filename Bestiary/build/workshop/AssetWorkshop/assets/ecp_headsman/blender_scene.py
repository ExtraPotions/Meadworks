"""The headsman's fight in Blender, from the Unity bake (unity/Assets/Editor/Headsman/HeadsmanBake):

    blender --background --factory-startup --python assets/ecp_headsman/blender_scene.py -- [--bake <folder>] [--name <blend>]

The same builds the player preview of the Executioner's Greataxe (Workshop.Greataxe.GreataxePreview, build.ps1 -Player):
a bake with the player's body, the axe in its hand and a dummy, and no thrown axe, forming axe or summons.

Reads <bake>/scene.json (the skinned skeletons: the boss, and the skeletons that rise where a thrown axe breaks), each
played by a Mesh Cache modifier on its .pc2 point cache, and <bake>/rigid.json (the axe in the fists, the thrown axe,
the axe forming, bone shards, rocks, the stand-in targets), each keyed frame by frame from its baked matrices and
hidden (scaled to nothing) while it is not shown. Markers name each attack and its moments; sequence.json binds a
camera to each part (close for the melee attacks, wide for the throws, from the side for the rear strike). Adds a
ground, a sun and a sky, and saves <bake>/headsman_moves.blend with the 3D views in material preview through the
camera. Nothing here goes into a bundle: the Skeleton is the game's, from the reference export.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
from workshop import materials, prefab, scene  # noqa: E402
sys.path.insert(0, HERE)
import blender_fx  # noqa: E402
import blender_reveal  # noqa: E402
import blender_shatter  # noqa: E402

# Unity's (x, y, z) is Blender's (-x, -z, y): the boss faces Blender's -Y (its right is -X), the far target is 10 m
# that way. Close: its front right, where the axe is carried; wide: over its right shoulder along the throw; rear: its
# right side, to see it strike behind and turn round.
CAMERAS = {
    'close': ((Vector((-3.6, -4.4, 2.2)), Vector((0.0, -0.4, 1.1))), 40),
    'wide': ((Vector((-3.0, 3.6, 2.7)), Vector((0.2, -5.0, 1.0))), 32),
    'rear': ((Vector((-5.8, -0.4, 2.2)), Vector((0.0, 0.0, 1.2))), 40),
    # The player preview (build.ps1 -Player): close on the stances, then the combo, which carries the player about
    # 1.8 m forward (-Y) towards the dummy at 2.8 m, from behind over the right shoulder (as the game's camera sees a
    # player), from the front right and from the side.
    'p_close': ((Vector((-2.3, -3.1, 1.6)), Vector((0.0, -0.2, 1.0))), 40),
    'p_game': ((Vector((-0.9, 3.6, 2.3)), Vector((0.0, -1.6, 1.1))), 30),
    'p_front': ((Vector((-3.0, -5.6, 1.9)), Vector((0.0, -1.3, 1.0))), 35),
    'p_side': ((Vector((-5.2, -1.4, 1.6)), Vector((0.0, -1.4, 1.0))), 35),
}

# The game's own props round the arena (reference only), clear of the fight and of the throw's line to the far target.
PROPS = [("world/Locations/Meadows/Dolmen01.prefab", Vector((5.0, -13.5, 0.0)), 15),
         ("world/Props/PineTree/Pinetree_01.prefab", Vector((8.0, -17.0, 0.0)), 40),
         ("world/Props/PineTree/Pinetree_01.prefab", Vector((-9.0, -15.0, 0.0)), 110),
         ("world/Props/PineTree/Pinetree_01.prefab", Vector((-12.0, 9.0, 0.0)), 200),
         ("world/Props/FirTree/FirTree_small_dead.prefab", Vector((5.5, -6.5, 0.0)), 0),
         ("GameElements/Pieces/skull_pile.prefab", Vector((2.4, -2.8, 0.0)), 30),
         ("Characters/Skeleton/model/Model/skeleton_pile.prefab", Vector((2.2, 2.0, 0.0)), -20),
         ("world/dungeon/Crypt/crypt_skeleton_laying.prefab", Vector((-2.6, -7.0, 0.0)), 80)]


def main():
    folder = _argument('--bake', os.path.join(HERE, 'out', 'blender'))
    data = json.load(open(os.path.join(folder, 'scene.json'), encoding='utf-8'))
    rigid = json.load(open(os.path.join(folder, 'rigid.json'), encoding='utf-8'))
    cuts = json.load(open(os.path.join(folder, 'sequence.json'), encoding='utf-8'))
    scene.clear()
    for part in data['parts']:
        _skinned(part, folder)
    shown = {}
    for part in rigid['parts']:
        obj, shown[part['name']] = _rigid(part)
        if part['name'].startswith('light_'):
            blender_fx.light(obj, shown[part['name']], _key)
            obj.visible_camera = obj.visible_shadow = False   # only its light shows
    if 'axe_ghost' in shown:
        _forming_axe(shown['axe_ghost'])
    if 'axe_thrown' in shown:
        _summons(rigid, cuts)
    cameras = _stage()
    _timeline(data, cuts, cameras)
    print(f"WORKSHOP sounds: {blender_fx.sounds(cuts, data['fps'])} strips")
    _views()
    path = os.path.join(folder, _argument('--name', 'headsman_moves') + '.blend')
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP blend {path}: {len(data['parts'])} skinned, {len(rigid['parts'])} rigid, {data['frames']} frames")


def _forming_axe(scales):
    """The new axe forming (baked plain): the real axe's look, its pieces revealed one at a time as it grows."""
    ghost = bpy.data.objects['axe_ghost']
    ghost.data.materials[0] = bpy.data.objects['axe_held'].data.materials[0]
    blender_reveal.axe(ghost, scales)


def _summons(rigid, cuts):
    """Where a thrown axe broke: its own pieces scatter, swirl and strike into the new skeleton one at a time, each
    where one of its bones is fading in, feet first."""
    thrown = next(p for p in rigid['parts'] if p['name'] == 'axe_thrown')
    m = thrown['matrices']
    matrices = [Matrix([m[16 * f + 4 * r:16 * f + 4 * r + 4] for r in range(4)]) for f in range(len(thrown['active']))]
    axe = bpy.data.objects['axe_thrown']
    made = blender_shatter.pieces(axe.data)
    for k, hit in enumerate(cuts.get('summonHit', [])):
        form, solid = cuts['summonForm'][k] + 1, cuts['summonSolid'][k] + 1
        first, final, where = blender_reveal.skeleton(bpy.data.objects[cuts['summonBody'][k]], form, solid)
        landings = blender_shatter.landings(len(made), first, final, hit)
        summon = {'hit': hit, 'index': k, 'form': form, 'landings': landings, 'targets': [where(f) for f in landings],
                  'spot': cuts['summonSpot'][3 * k:3 * k + 3]}
        blender_shatter.animate(made, axe.data.materials[0], matrices, summon, _key)
        # The pieces strike in silence: a knock each (29 of them) sounded like a door, the user said.
    print(f"WORKSHOP summons: {len(cuts.get('summonHit', []))}, {len(made)} pieces each")


def _argument(name, default):
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    return argv[argv.index(name) + 1] if name in argv else default


def _mesh(name, vertices, triangles, uvs):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([vertices[i:i + 3] for i in range(0, len(vertices), 3)], [],
                     [triangles[i:i + 3] for i in range(0, len(triangles), 3)])
    if uvs:
        layer = mesh.uv_layers.new(name='uv')
        for loop in mesh.loops:
            layer.data[loop.index].uv = (uvs[2 * loop.vertex_index], uvs[2 * loop.vertex_index + 1])
    return mesh


def _object(name, mesh, part):
    mesh.materials.append(_material(part))
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    scene.select_only([obj])
    bpy.ops.object.shade_smooth_by_angle(angle=0.6)
    return obj


def _skinned(part, folder):
    """One baked skinned renderer: its mesh, UVs and texture, played by its point cache."""
    obj = _object(part['name'], _mesh(part['name'], part['vertices'], part['triangles'], part['uvs']), part)
    cache = obj.modifiers.new('bake', 'MESH_CACHE')
    cache.cache_format = 'PC2'
    cache.filepath = os.path.join(folder, part['cache'])
    cache.frame_start = 1.0


def _rigid(part):
    """One baked rigid renderer: its mesh once, keyed frame by frame from its matrices, scaled to nothing when hidden."""
    obj = _object(part['name'], _mesh(part['name'], part['vertices'], part['triangles'], part['uvs']), part)
    obj.rotation_mode = 'QUATERNION'
    m, active = part['matrices'], part['active']
    keys = {path: [[] for _ in range(size)] for path, size in (('location', 3), ('rotation_quaternion', 4), ('scale', 3))}
    last, scales = None, []
    for f, shown in enumerate(active):
        loc, rot, size = Matrix([m[16 * f + 4 * r:16 * f + 4 * r + 4] for r in range(4)]).decompose()
        if last is not None and rot.dot(last) < 0.0:
            rot.negate()
        last = rot
        scales.append(size.x if shown else 0.0)
        for path, value in (('location', loc), ('rotation_quaternion', rot), ('scale', size if shown else Vector((0, 0, 0)))):
            for i, v in enumerate(value):
                keys[path][i] += [f + 1, v]
    _key(obj, keys)
    return obj, scales


def _key(owner, keys):
    """Every frame's keys at once (an object's or a light's): one key through the API makes the curves, then each is
    filled in bulk."""
    for path in keys:
        owner.keyframe_insert(path, frame=1)
    for curve in _curves(owner):
        values = keys[curve.data_path][curve.array_index]
        points = curve.keyframe_points
        points.add(len(values) // 2 - len(points))
        points.foreach_set('co', values)
        points.foreach_set('interpolation', [0] * (len(values) // 2))   # CONSTANT: one key per frame
        curve.update()


def _curves(obj):
    action = obj.animation_data.action
    if hasattr(action, 'layers') and action.layers:
        slot = obj.animation_data.action_slot
        for layer in action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(slot)
                if bag is not None:
                    return list(bag.fcurves)
    return list(action.fcurves)


def _material(part):
    texture = part['texture']
    name = os.path.splitext(os.path.basename(texture))[0] if texture else part['name'].split('.')[0].rstrip('_0123456789')
    if name in bpy.data.materials:
        return bpy.data.materials[name]
    mat = materials.flat(name, tuple(part['color']), roughness=0.85)
    bsdf = materials.principled(mat)
    if part['glow']:
        bsdf.inputs['Emission Color'].default_value = (*part['color'], 1.0)
        bsdf.inputs['Emission Strength'].default_value = 5.0
    if texture and os.path.exists(texture):
        image = mat.node_tree.nodes.new('ShaderNodeTexImage')
        image.image = bpy.data.images.load(texture, check_existing=True)
        image.interpolation = 'Closest'   # the game's textures are point filtered
        mat.node_tree.links.new(image.outputs['Color'], bsdf.inputs['Base Color'])
    return mat


def _stage():
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, -4, 0))
    ground = bpy.context.active_object
    ground.name = 'ground'
    ground.data.materials.append(_earth())
    _props()
    sun = bpy.data.objects.new('sun', bpy.data.lights.new('sun', 'SUN'))
    sun.data.energy = 4.0
    sun.rotation_euler = (0.6, 0.25, -0.7)
    bpy.context.collection.objects.link(sun)
    world = bpy.data.worlds.new('sky')
    world.use_nodes = True
    background = next(n for n in world.node_tree.nodes if n.type == 'BACKGROUND')
    background.inputs[0].default_value = (0.32, 0.38, 0.42, 1.0)
    background.inputs[1].default_value = 0.8
    bpy.context.scene.world = world
    return {name: _camera(name, eye, look, lens) for name, ((eye, look), lens) in CAMERAS.items()}


def _earth():
    """Black Forest earth: dark brown with mossy patches, nothing to catch the eye."""
    mat = materials.flat('earth', (0.06, 0.05, 0.035), roughness=0.95)
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    noise = nodes.new('ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 0.8
    ramp = nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color = (0.045, 0.038, 0.026, 1.0)
    ramp.color_ramp.elements[1].color = (0.060, 0.075, 0.035, 1.0)
    links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], materials.principled(mat).inputs['Base Color'])
    return mat


def _props():
    for path, at, turn in PROPS:
        try:
            root = prefab.load(path, root_name=os.path.basename(path).split('.')[0])
        except Exception as error:   # a prop the loader cannot read is left out, not fatal
            print(f"WORKSHOP prop {path} left out: {error}")
            continue
        root.matrix_world = Matrix.Translation(at) @ Matrix.Rotation(math.radians(turn), 4, 'Z')
        if 'Tree' in path:   # tall trees would throw their shade over the whole fight
            for obj in root.children_recursive:
                obj.visible_shadow = False


def _camera(name, eye, look, lens):
    camera = bpy.data.objects.new('camera_' + name, bpy.data.cameras.new('camera_' + name))
    camera.data.lens = lens
    camera.location = eye
    camera.rotation_euler = (look - eye).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(camera)
    return camera


def _timeline(data, cuts, cameras):
    s = bpy.context.scene
    s.render.fps = int(round(data['fps']))
    s.frame_start, s.frame_end, s.frame_current = 1, data['frames'], 1
    markers = {}
    for name, frame in zip(data['markerNames'], data['markerFrames']):
        markers.setdefault(frame + 1, s.timeline_markers.new(name, frame=frame + 1))
    for frame, name in zip(cuts['frames'], cuts['names']):
        marker = markers.get(frame + 1) or s.timeline_markers.new('cut', frame=frame + 1)
        marker.camera = cameras[name]
    s.camera = cameras[cuts['names'][0]]


def _views():
    """Every 3D view in material preview, looking through the camera, overlays off."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.shading.type = 'MATERIAL'
                    space.overlay.show_floor = False
                    space.overlay.show_overlays = False
                    if space.region_3d is not None:
                        space.region_3d.view_perspective = 'CAMERA'


main()
