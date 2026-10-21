"""The skull at the top of the spine, looking out of the blade's flat (+X): cranium and face in one piece, eye sockets
and nose cut dark into it, brow ridge, cheekbones, upper teeth, and the jaw hanging a little open on its hinges.

Skull coordinates: origin in the cranium, +X the face, +Y across (towards the axe's back), +Z up; the head module
moves the finished bone into axe coordinates. Sizes are a big man's skull, about 1.1 times life.
"""
import math

import bpy
from mathutils import Matrix, Vector

import axe_mesh as geo

CRANIUM = ((-0.015, 0.0, 0.018), (0.100, 0.078, 0.088))    # centre, half sizes
FACE = [(0.022, 0.028, 0.062, 0.060), (-0.004, 0.034, 0.066, 0.063), (-0.034, 0.040, 0.067, 0.057),
        (-0.060, 0.036, 0.050, 0.063), (-0.084, 0.030, 0.037, 0.068)]   # (z, half depth, half width, centre x)
EYES = [((0.089, side * 0.032, -0.006), (0.026, 0.027, 0.024), side * 6) for side in (-1, 1)]   # at, size, tilt
NOSE = ((0.101, 0.0, -0.043), (0.020, 0.010, 0.022), 0)
HINGE = Vector((0.004, 0.0, -0.040))
JAW_OPEN = math.radians(9)


def build(mats, matrix):
    """The whole skull, placed by `matrix` (skull coordinates to axe coordinates)."""
    bone, dark = mats["skull"], mats["socket"]
    head = _cranium_and_face(bone, dark)
    parts = [head, _brow(bone)] + _cheekbones(bone) + _upper_teeth(bone)
    jaw = _jaw(bone)
    open_jaw = Matrix.Translation(HINGE) @ Matrix.Rotation(JAW_OPEN, 4, 'Y') @ Matrix.Translation(-HINGE)
    for part in jaw:
        geo.place(part, open_jaw)
    for part in parts + jaw:
        geo.place(part, matrix)
    return parts + jaw


def _cranium_and_face(bone, dark):
    centre, half = CRANIUM
    cranium = geo.knob("skull", centre, half, bone, segments=14, rings=9)
    stations = [(z, depth, width, cx, 0.0) for z, depth, width, cx in FACE]
    face = geo.loft_z("skull_face", stations, bone, sides=10, power=0.8)
    _boolean(cranium, face, 'UNION')
    for i, (at, size, tilt) in enumerate(EYES + [NOSE]):
        tilted = Matrix.Translation(at) @ Matrix.Rotation(math.radians(tilt), 4, 'X')
        _boolean(cranium, geo.knob(f"skull_cut_{i}", (0, 0, 0), size, dark, 10, 6, tilted), 'DIFFERENCE')
    return cranium


def _boolean(target, tool, operation):
    """Applies tool to target with an exact boolean (keeping the tool's material on the faces it makes), then
    removes the tool."""
    mod = target.modifiers.new("cut", 'BOOLEAN')
    mod.object, mod.operation, mod.solver = tool, operation, 'EXACT'
    mod.material_mode = 'TRANSFER'
    depsgraph = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(target.evaluated_get(depsgraph))
    target.modifiers.remove(mod)
    old, target.data = target.data, mesh
    bpy.data.meshes.remove(old)
    bpy.data.objects.remove(tool, do_unlink=True)


def _brow(mat):
    """A plain ridge over both eyes, like the game's skulls: no scowl."""
    points = [(0.068, -0.062, 0.012), (0.088, -0.034, 0.020), (0.095, 0.000, 0.016), (0.088, 0.034, 0.020),
              (0.068, 0.062, 0.012)]
    radii = [(0.005, 0.004), (0.007, 0.005), (0.006, 0.005), (0.007, 0.005), (0.005, 0.004)]
    return geo.sweep("skull_brow", points, radii, mat, hint=(0, 0, 1), sides=6, smooth=True)


def _cheekbones(mat):
    parts = []
    for side in (-1, 1):
        points = [(0.080, side * 0.058, -0.030), (0.050, side * 0.071, -0.030), (0.012, side * 0.072, -0.024)]
        radii = [(0.008, 0.010), (0.006, 0.008), (0.005, 0.006)]
        parts.append(geo.sweep(f"skull_cheek_{side}", points, radii, mat, hint=(0, 0, 1), sides=4, smooth=True))
    return parts


def _upper_teeth(mat):
    return _teeth("skull_tooth_up", (0.052, 0.0, -0.090), (0.044, 0.033), 8, -1, mat)


def _teeth(name, centre, radii, count, direction, mat):
    """A row of blunt, square teeth round a dental arch, the front ones longest, pointing up (1) or down (-1)."""
    parts = []
    for i in range(count):
        angle = math.radians(-78 + 156 * i / (count - 1))
        front = math.cos(angle)
        at = Vector(centre) + Vector((radii[0] * front, radii[1] * math.sin(angle), 0.0))
        length = 0.011 + 0.006 * front
        tip = at + Vector((0.002 * front, 0.0, direction * length))
        width = 0.0065 + 0.0015 * (1 - front)
        points = [at - Vector((0, 0, direction * 0.004)), tip]
        sizes = [(width, width * 0.8), (width * 0.75, width * 0.6)]
        parts.append(geo.sweep(f"{name}_{i}", points, sizes, mat, hint=(1, 0, 0), sides=4, turn=math.pi / 4,
                               smooth=False))
    return parts


def _jaw(mat):
    """The mandible: a bony U from angle to angle round the chin, the two rami up to the hinges, lower teeth."""
    body = [(0.000, -0.057, -0.098), (0.040, -0.050, -0.112), (0.078, -0.030, -0.120), (0.094, 0.000, -0.123),
            (0.078, 0.030, -0.120), (0.040, 0.050, -0.112), (0.000, 0.057, -0.098)]
    radii = [(0.007, 0.013), (0.007, 0.014), (0.007, 0.014), (0.008, 0.015), (0.007, 0.014), (0.007, 0.014), (0.007, 0.013)]
    parts = [geo.sweep("skull_jaw", body, radii, mat, hint=(0, 0, 1), sides=6, power=0.8, smooth=True)]
    for side in (-1, 1):
        ramus = [(-0.002, side * 0.057, -0.102), (0.000, side * 0.060, -0.070), (0.004, side * 0.060, -0.042)]
        sizes = [(0.007, 0.016), (0.006, 0.013), (0.006, 0.010)]
        parts.append(geo.sweep(f"skull_ramus_{side}", ramus, sizes, mat, hint=(1, 0, 0), sides=6, power=0.8, smooth=True))
    parts += _teeth("skull_tooth_down", (0.050, 0.0, -0.104), (0.040, 0.044), 8, 1, mat)
    return parts
