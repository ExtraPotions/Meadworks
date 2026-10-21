"""What the mimic adds to the chest: teeth on both jaws, gums, a throat, a tongue and two glowing eyes. The teeth wear
the game's Bone Fragments texture and the gums and tongue its uncooked bear meat, point-filtered like the game.

All of it sits inside the chest's volume when the rig is in its dormant pose (teeth, eyes and tongue scaled to nothing),
so the closed mimic is the plain crypt chest.
"""
import math
import random

import bmesh
import bpy

from workshop import materials, reference, scene, shapes
import rig as mimic_rig

FRONT, SIDE = -0.35, 0.83          # lower teeth rows: y of the front row, |x| of the side rows
TOP, UNDER = 0.543, 0.53           # base top surface, lid underside


BONE = "GameElements/Items/materials/_res/bonefragments/bonefragments_"
MEAT = "GameElements/Items/_res/BearMeat/model/bearmeat_"
TEETH_TILE, FLESH_TILE = 0.12, 0.35     # metres one repeat of the game texture covers


def build(rig):
    random.seed(11)
    bone = reference.material("mimic_teeth", BONE + "d.png", BONE + "n.png", roughness=0.5)
    flesh = reference.material("mimic_flesh", MEAT + "uncooked_d.png", MEAT + "n.png", roughness=0.3)
    mimic_rig.attach(_tiled(_teeth("teeth_lower", bone, up=True), TEETH_TILE), rig, "teeth_low")
    mimic_rig.attach(_tiled(_teeth("teeth_upper", bone, up=False), TEETH_TILE), rig, "teeth_up")
    for name, z, bone_name in (("gum_lower", TOP + 0.0065, "body"), ("gum_upper", UNDER - 0.006, "lid")):
        gum = shapes.box(name, (1.66, 0.70, 0.013), (0, 0.02, z), material=flesh)
        mimic_rig.attach(_tiled(gum, FLESH_TILE), rig, bone_name)
    throat = materials.flat("mimic_throat", (0.02, 0.0, 0.0), roughness=0.9)
    mimic_rig.attach(shapes.cylinder("throat", 0.17, 0.004, (0, 0.2, TOP + 0.015), material=throat, vertices=20), rig, "body")
    _eyes(rig)
    mimic_rig.skin(_tiled(_tongue(flesh), FLESH_TILE), rig)


def _tiled(obj, metres):
    return reference.uv_tiles(obj, metres)


def _teeth(name, material, up):
    """Rows of four-sided fangs along the front and both sides; the upper row sits between the lower teeth."""
    base, offset = (TOP, 0.0) if up else (UNDER, 0.075)
    spots = [(x, FRONT + (0.02 if not up else 0.0)) for x in _spread(-0.78 + offset, 0.78, 11 if up else 10)]
    for sx in (-1, 1):
        spots += [(sx * (SIDE - (0.02 if not up else 0.0)), y) for y in _spread(-0.25 + offset, 0.3, 4)]
    parts = [_fang(f"{name}_{i}", x, y, base, up, _height(x), material) for i, (x, y) in enumerate(spots)]
    return scene.join(parts, name)


def _spread(start, end, count):
    step = (end - start) / (count - 1)
    return [start + step * i for i in range(count)]


def _height(x):
    """Longer fangs near the corners, a little variation everywhere."""
    corner = 0.05 if abs(abs(x) - 0.6) < 0.1 else 0.0
    return 0.06 + corner + random.uniform(0.0, 0.025)


def _fang(name, x, y, base, up, height, material):
    tilt = (random.uniform(-0.12, 0.12), random.uniform(-0.12, 0.12), 0.0)
    rotation = (tilt[0], tilt[1], math.pi / 4) if up else (math.pi + tilt[0], tilt[1], math.pi / 4)
    z = base + height / 2 if up else base - height / 2
    bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=0.028, radius2=0.0, depth=height,
                                    location=(x, y, z), rotation=rotation)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(material)
    return obj


def _eyes(rig):
    glow = materials.flat("mimic_eye", (0.8, 1.0, 0.25))
    bsdf = materials.principled(glow)
    bsdf.inputs['Emission Color'].default_value = (0.75, 1.0, 0.2, 1.0)
    bsdf.inputs['Emission Strength'].default_value = 12.0
    for sx in (-1, 1):
        eye = shapes.sphere(f"eye_{'r' if sx > 0 else 'l'}", 0.042, (sx * 0.3, -0.34, 0.505), glow, segments=12, rings=8)
        mimic_rig.attach(eye, rig, "eyes")


def _tongue(material):
    """A flattened, tapering tongue lying on the lower gum, weighted along its three bones."""
    mesh = bpy.data.meshes.new("tongue")
    obj = bpy.data.objects.new("tongue", mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    rings = [_tongue_ring(bm, i / 12) for i in range(13)]
    for a, b in zip(rings, rings[1:]):
        for j in range(6):
            bm.faces.new((a[j], a[(j + 1) % 6], b[(j + 1) % 6], b[j]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bm.to_mesh(mesh)
    bm.free()
    obj.data.materials.append(material)
    _tongue_weights(obj)
    return obj


def _tongue_ring(bm, s):
    """Cross-section at s (0 root, 1 tip): a flattened hexagon that narrows towards a rounded tip."""
    y = 0.30 - 0.66 * s
    width = 0.36 * (1 - 0.3 * s) * (math.sqrt(max(1 - s, 0.0)) * 0.6 + 0.4 if s > 0.85 else 1.0)
    thick, z = 0.03 * (1 - 0.3 * s), 0.572
    points = ((-0.5, 0), (-0.3, 0.5), (0.3, 0.5), (0.5, 0), (0.3, -0.5), (-0.3, -0.5))
    return [bm.verts.new((px * width, y, z + pz * thick)) for px, pz in points]


def _tongue_weights(obj):
    """Each vertex follows the bone it lies over, blending across the joints at y 0.06 and -0.15."""
    groups = [obj.vertex_groups.new(name=f"tongue_{i}") for i in (1, 2, 3)]
    for vertex in obj.data.vertices:
        y = vertex.co.y
        for group, (start, end) in zip(groups, ((0.40, 0.06), (0.06, -0.15), (-0.15, -0.50))):
            weight = _span_weight(y, start, end, 0.05)
            if weight > 0:
                group.add([vertex.index], weight, 'REPLACE')


def _span_weight(y, start, end, blend):
    """1 inside [end, start], fading to 0 over `blend` metres past either joint."""
    inside = min(start - y, y - end)
    return max(0.0, min(1.0, 0.5 + inside / (2 * blend)))
