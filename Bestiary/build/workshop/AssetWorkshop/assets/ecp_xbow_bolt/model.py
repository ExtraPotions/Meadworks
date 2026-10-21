"""The Blunted Bone Bolt (Elite Creatures Pack): a thin bone shaft grimed like the skeleton itself and a blunt knuckle
of bone for a head, seated on bone; no feathers (it does blunt damage; 2026-09-29, the user: "a blunt tip and no
feathers", "the same length as the vanilla bone bolt", whose mesh is 0.573 m). One model for every bolt the mod shows:
the players' Blunted Bone Bolts in the hand, on the ground and in flight, and the crossbowman's in the groove, the
fingers and the quiver.

The origin is the nock end on the shaft's line and the point is forward (-Y, Unity +Z), LENGTH away.
"""
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "ecp_skel_arsenal"))
import low_paint as gp

import bmesh
import bpy

from workshop import materials, shapes

TEXTURE_SIZE = 32
CATEGORY = "ammo.bolt"
NORMAL_MAP = False
AO_STRENGTH = 0.2

LENGTH = 0.57
HEAD = LENGTH - 0.43       # the head's parts are placed as on the first, 0.43 m bolt, moved forward by this
SHAFT = [(0.000, 0.0048), (-0.030, 0.0056), (-0.210, 0.0052), (-0.440, 0.0050), (-0.500, 0.0060)]   # (y, radius)


def build():
    bone, point, sinew = _bone(), _point_bone(), _sinew()
    _shaft(bone)
    _knob("nock", (0.0, 0.002, 0.0), 0.0062, bone)
    _blunt(point)
    for name, y0, y1 in (("bind_point", -0.364 - HEAD, -0.350 - HEAD), ("bind_nock", -0.016, -0.008)):
        _wrap(name, y0, y1, sinew)


def _shaft(material, sides=6):
    mesh = bmesh.new()
    rings = [[mesh.verts.new((r * math.cos(a), y, r * math.sin(a)))
              for a in (2 * math.pi * k / sides for k in range(sides))] for y, r in SHAFT]
    for a, b in zip(rings, rings[1:]):
        for i in range(sides):
            j = (i + 1) % sides
            mesh.faces.new((a[i], a[j], b[j], b[i]))
    mesh.faces.new(list(reversed(rings[0])))
    mesh.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    _object("shaft", mesh, material)


def _blunt(material):
    """A blunt head, for crushing rather than piercing: a knuckle of bone swelling out of a thick socket, rounded in front
    and a little flattened at the tip, like a hunter's blunt."""
    shapes.cylinder("socket", 0.0078, 0.034, (0.0, -0.372 - HEAD, 0.0), (math.radians(90), 0.0, 0.0), material, vertices=6)
    _knob("knuckle", (0.0, -0.405 - HEAD, 0.0), 0.0135, material, squash=(1.0, 1.25, 0.95))
    _knob("knuckle_face", (0.0, -0.421 - HEAD, 0.0), 0.0105, material, squash=(1.05, 0.55, 1.0))
    for side in (-1, 1):
        _knob(f"knuckle_lobe_{side}", (side * 0.008, -0.408 - HEAD, 0.002), 0.008, material)


def _wrap(name, y0, y1, material):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.0068, depth=y1 - y0, location=(0.0, (y0 + y1) / 2, 0.0),
                                        rotation=(math.radians(90), 0.0, 0.0))
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(material)


def _knob(name, location, radius, material, squash=(1.0, 0.7, 1.0)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=6, ring_count=3, radius=radius, location=location)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = squash
    obj.data.materials.append(material)


def _object(name, mesh, material):
    data = bpy.data.meshes.new(name)
    mesh.to_mesh(data)
    mesh.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material)


def _bone():
    return gp.bone('bolt_bone')


def _point_bone():
    return gp.teeth('bolt_point')


def _sinew():
    return gp.sinew('bolt_joint_bone')
