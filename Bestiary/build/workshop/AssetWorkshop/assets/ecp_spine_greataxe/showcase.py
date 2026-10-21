"""The spine greataxe in the hands of the game's own Skeleton, as a Blender scene to look round:

    blender --background --factory-startup --python assets/ecp_spine_greataxe/showcase.py -- [--render]
    blender assets/ecp_spine_greataxe/out/showcase/greataxe_showcase.blend --python assets/ecp_spine_greataxe/showcase_open.py

Needs the asset built first (build.ps1 -Asset ecp_spine_greataxe): it takes the baked axe from out/. Two skeletons,
posed through IK whose hand targets ride the axe: a sentinel with the axe planted at its side, one hand on the haft,
and one in a two-handed guard, the head of the axe over its right shoulder. A third axe stands alone on the right.
Round them, some of the game's own props (standing stones, pines, bone and skull piles) on dark earth under a warm low
sun, and the axes' atlas point filtered as the game samples it, so the axe is judged in the game's company. Cameras:
'camera' (all), 'camera_head' (the axe's head), 'camera_guard' (the guard, low). The Skeleton and the props come from
the reference export and are tagged reference: nothing here goes into a bundle. --render writes stills to
out/showcase/.
"""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
sys.dont_write_bytecode = True

import bpy  # noqa: E402
from mathutils import Matrix, Vector  # noqa: E402

import axe_head  # noqa: E402
import axe_spine  # noqa: E402
import model  # noqa: E402
import showcase_pose as pose  # noqa: E402
import showcase_rig  # noqa: E402
from workshop import materials, prefab, scene  # noqa: E402

OUT = os.path.join(HERE, 'out', 'showcase')
ASSET = os.path.join(HERE, 'out', 'ecp_spine_greataxe.blend')
SENTINEL, GUARD, DISPLAY = (1.05, 0.0), (-1.25, 0.0), (3.05, 0.55)
PROPS = [("world/Locations/Meadows/Dolmen01.prefab", Vector((0.6, 9.0, 0.0)), 15),
         ("GameElements/Pieces/skull_pile.prefab", Vector((-2.35, 0.9, 0.0)), 30),
         ("Characters/Skeleton/model/Model/skeleton_pile.prefab", Vector((2.1, 0.8, 0.0)), -20),
         ("world/dungeon/Crypt/crypt_skeleton_laying.prefab", Vector((-0.2, 1.6, 0.0)), 80),
         ("world/Props/FirTree/FirTree_small_dead.prefab", Vector((-4.5, 6.0, 0.0)), 0),
         ("world/Props/PineTree/Pinetree_01.prefab", Vector((5.5, 11.0, 0.0)), 40),
         ("world/Props/PineTree/Pinetree_01.prefab", Vector((-7.0, 13.0, 0.0)), 110)]
CAMERAS = {"camera": ((1.0, -6.6, 1.75), (0.35, 0.0, 1.15), 30),
           "camera_guard": ((0.2, -3.4, 1.0), (-1.45, -0.2, 1.35), 32)}


class Grips:
    """The axe's two grips in its own coordinates: (point, haft direction, radius)."""

    def __init__(self):
        centre = axe_spine.Centre()
        pivot = centre.at(model.LOWER_GRIP)[0]
        self.lower = self._grip(centre, model.LOWER_GRIP, pivot, 0.040)
        self.upper = self._grip(centre, model.UPPER_GRIP, pivot, 0.036)
        self.butt = centre.at(0.0)[0] - pivot
        self.skull = centre.at(axe_spine.top(centre))[0] + axe_head.above_spine() - pivot

    @staticmethod
    def _grip(centre, s, pivot, radius):
        point, side, back, up = centre.at(s)
        return point - pivot, up, radius


def main():
    scene.clear()
    grips = Grips()
    axes = [_axe(n) for n in ("axe_sentinel", "axe_guard", "axe_display")]
    _sentinel(axes[0], grips)
    _guard(axes[1], grips)
    _display(axes[2], grips)
    _stage(axes[1].matrix_world @ grips.skull)
    _views()
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.file.pack_all()
    bpy.context.preferences.filepaths.save_version = 0
    path = os.path.join(OUT, 'greataxe_showcase.blend')
    bpy.ops.wm.save_as_mainfile(filepath=path)
    print(f"WORKSHOP showcase {path}")
    if '--render' in sys.argv:
        _render()


def _axe(name):
    """A copy of the baked axe (the mesh and atlas the bundle would carry)."""
    if 'ecp_spine_greataxe' not in bpy.data.meshes:
        with bpy.data.libraries.load(ASSET, link=False) as (source, target):
            target.meshes = ['ecp_spine_greataxe']
    for node in bpy.data.meshes['ecp_spine_greataxe'].materials[0].node_tree.nodes:
        if node.type == 'TEX_IMAGE':
            node.interpolation = 'Closest'   # the game point-filters its textures
    obj = bpy.data.objects.new(name, bpy.data.meshes['ecp_spine_greataxe'])
    bpy.context.collection.objects.link(obj)
    return obj


def _standing(axe, grips, where, lean, facing):
    """The axe upright on its butt at world (x, y): its skull looking along `facing` degrees (0: -Y), leaning
    `lean` degrees towards +X."""
    turn = Matrix.Rotation(math.radians(lean), 4, 'Y') @ Matrix.Rotation(math.radians(facing - 90), 4, 'Z')
    butt = turn @ grips.butt
    axe.matrix_world = Matrix.Translation(Vector((where[0], where[1], 0.0)) - butt) @ turn


def _sentinel(axe, grips):
    """On guard: the axe planted at its right side, the right hand on the upper grip, the left arm down."""
    x, y = SENTINEL
    rig, _ = showcase_rig.skeleton("sentinel", (x, y, 0.0))
    _standing(axe, grips, (x - 0.34, y - 0.12), -12.0, 0.0)
    pose.turn(rig, "LeftArm", (0, 1, 0), 74)
    pose.turn(rig, "LeftForeArm", (0, 1, 0), 10)
    pose.turn(rig, "LeftArm", (1, 0, 0), -6)
    pose.curl(rig, "Left", 0.45)
    pose.turn(rig, "Head", (0, 0, 1), -14)
    pose.turn(rig, "Head", (1, 0, 0), -8)
    point, axis, radius = grips.upper
    pose.hold(rig, "Right", axe, point + axis * 0.07, axis, radius)
    pose.plant(rig, "Left", (x + 0.17, y + 0.02), -8)
    pose.plant(rig, "Right", (x - 0.15, y - 0.02), 10)
    pose.settle_poles(rig)


def _guard(axe, grips):
    """A two-handed guard: crouched, the left hand low on the lower grip, the right on the upper, the head of the axe
    up over the right shoulder with its edge out and the skull looking at you."""
    x, y = GUARD
    rig, _ = showcase_rig.skeleton("guard", (x, y, 0.0))
    pose.shift(rig, "Hips", (0.0, -0.02, -0.09))
    pose.turn(rig, "Spine", (1, 0, 0), 10)
    pose.turn(rig, "Spine1", (0, 0, 1), -10)
    pose.turn(rig, "Head", (1, 0, 0), -12)
    pose.turn(rig, "Head", (0, 0, 1), 8)
    low_hand = Vector((x + 0.20, y - 0.40, 1.00))
    haft = Vector((-0.44, -0.06, 0.90)).normalized()
    _along(axe, grips, low_hand, haft, Vector((0, -1, 0)))
    for side, grip in (("Left", grips.lower), ("Right", grips.upper)):
        point, axis, radius = grip
        pose.hold(rig, side, axe, point, axis, radius)
    pose.plant(rig, "Left", (x + 0.22, -0.22), -18)
    pose.plant(rig, "Right", (x - 0.24, 0.20), 22)
    pose.settle_poles(rig)


def _along(axe, grips, low_hand, haft, face):
    """Places the axe with its lower grip at `low_hand`, the line of its grips along `haft` and its skull looking
    as near `face` as that allows."""
    lower, upper = grips.lower[0], grips.upper[0]
    local_up = (upper - lower).normalized()
    local_face = Vector((1, 0, 0))
    local_face = (local_face - local_up * local_face.dot(local_up)).normalized()
    world_face = (face - haft * face.dot(haft)).normalized()
    local = Matrix((local_up, local_face, local_up.cross(local_face))).transposed()
    world = Matrix((haft, world_face, haft.cross(world_face))).transposed()
    turn = (world @ local.inverted()).to_4x4()
    axe.matrix_world = Matrix.Translation(low_hand - turn @ lower) @ turn


def _display(axe, grips):
    """The axe alone, upright on a low stone, skull to the front."""
    x, y = DISPLAY
    bpy.ops.mesh.primitive_cylinder_add(vertices=10, radius=0.32, depth=0.14, location=(x, y, 0.07))
    stone = bpy.context.active_object
    stone.name = 'display_stone'
    stone.data.materials.append(materials.stone('display_stone', scale=4.0))
    _standing(axe, grips, (x, y), 0.0, 20.0)
    axe.location.z += 0.14


def _stage(skull):
    """Dark earth, the game's own props round about, a warm low sun and a cool fill, a hazy grey-blue sky, and the
    cameras (the head's framing its skull)."""
    bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = 'ground'
    ground.data.materials.append(_earth())
    _props()
    _light('sun', 'SUN', 3.6, (math.radians(62), 0.0, math.radians(-40)), (1.0, 0.82, 0.60))
    _light('fill', 'SUN', 0.8, (math.radians(55), 0.0, math.radians(150)), (0.50, 0.64, 1.0))
    _sky()
    for name, (at, look, lens) in CAMERAS.items():
        _camera(name, Vector(at), Vector(look), lens)
    _camera("camera_head", skull + Vector((0.55, -1.45, 0.25)), skull + Vector((-0.12, 0.0, -0.08)), 50)
    bpy.context.scene.camera = bpy.data.objects['camera']


def _earth():
    """Black Forest earth: dark brown, mossy patches, no pattern to catch the eye."""
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
    """A few of the game's own props (reference only): standing stones behind, bone piles and a laid-out dead man at
    the skeletons' feet, a dead fir and pines at the back."""
    for path, at, turn in PROPS:
        try:
            root = prefab.load(path, root_name=os.path.basename(path).split('.')[0])
        except Exception as error:   # a prop the loader cannot read is left out, not fatal
            print(f"WORKSHOP prop {path} left out: {error}")
            continue
        root.matrix_world = Matrix.Translation(at) @ Matrix.Rotation(math.radians(turn), 4, 'Z')


def _sky():
    """A hazy grey-blue sky (a world mist would swallow the sun in EEVEE)."""
    world = bpy.data.worlds.new('sky')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs[0].default_value = (0.30, 0.35, 0.38, 1.0)
    world.node_tree.nodes['Background'].inputs[1].default_value = 0.85
    bpy.context.scene.world = world


def _light(name, kind, energy, rotation, colour):
    light = bpy.data.objects.new(name, bpy.data.lights.new(name, kind))
    light.data.energy, light.data.color = energy, colour
    light.rotation_euler = rotation
    bpy.context.collection.objects.link(light)


def _camera(name, at, look, lens):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    cam.data.lens = lens
    cam.location = at
    cam.rotation_euler = (look - at).to_track_quat('-Z', 'Y').to_euler()
    bpy.context.collection.objects.link(cam)


def _views():
    """Every 3D view in material preview with the scene's own lights and sky, looking through the main camera."""
    for screen in bpy.data.screens:
        for area in screen.areas:
            for space in area.spaces:
                if space.type == 'VIEW_3D':
                    space.shading.type = 'MATERIAL'
                    space.shading.use_scene_lights = True
                    space.shading.use_scene_world = True
                    space.overlay.show_floor = False
                    space.overlay.show_extras = False
                    if space.region_3d is not None:
                        space.region_3d.view_perspective = 'CAMERA'


def _render():
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.eevee.taa_render_samples = 48
    s.render.resolution_x, s.render.resolution_y = 1600, 1000
    s.view_settings.view_transform = 'AgX'
    for name in list(CAMERAS) + ["camera_head"]:
        s.camera = bpy.data.objects[name]
        s.render.filepath = os.path.join(OUT, f"{name}.png")
        bpy.ops.render.render(write_still=True)
    s.camera = bpy.data.objects['camera']


main()
