"""One large bone crudely carved into an arrow with short integral bone fins.
Nock at origin, tip -Y (Unity +Z); no feathers, bindings or attached head.
"""
import math
import os
import sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'ecp_skel_arsenal'))
import bmesh
from mathutils import Vector
import grave_blade as gb
import low_paint as gp
import low_shapes as gs

TEXTURE_SIZE = 64
AO_STRENGTH = 0.2
CATEGORY = 'ammo.arrow'
NORMAL_MAP = False
LENGTH = 1.16


def build():
    bone = gp.bone('carved_arrow_bone')
    # Continuous shaft and point: broad uneven carving planes and two edge chips.
    sections = [
        (-.009, -.0058, .0054, .0048, .25, 1.0),
        (.025, -.0062, .0060, .0053, .25, 1.0),
        (.15, -.0058, .0057, .0054, .25, 1.0),
        (.58, -.0064, .0062, .0057, .25, 1.0),
        (.97, -.0058, .0060, .0053, .25, 1.0),
        (1.014, -.0120, .0105, .0060, .27, 1.08),
        (1.037, -.0155, .0140, .0056, .28, 1.05),
        (1.060, -.0140, .0130, .0050, .30, 1.12),
        (1.066, -.0135, .0090, .0047, .30, 1.06),
        (1.073, -.0130, .0118, .0044, .28, 1.10),
        (1.088, -.0115, .0100, .0039, .30, 1.02),
        (1.095, -.0077, .0092, .0036, .28, 1.08),
        (1.103, -.0098, .0083, .0032, .30, 1.10),
        (1.136, -.0048, .0040, .0018, .29, 1.05),
    ]
    gb.blade('carved_bone_shaft_and_point', sections, bone, tip=(LENGTH, -.0012))
    for i in range(3):
        _fin(f'carved_bone_fin_{i}', math.radians(90 + 120*i), bone, i)


def _fin(name, angle, material, index):
    """Short solid remnants of the parent bone; uneven chisel-cut perimeter."""
    outward = Vector((math.cos(angle), 0, math.sin(angle)))
    across = Vector((-math.sin(angle), 0, math.cos(angle)))
    shift = (.0, .006, -.004)[index]
    height = (1.0, .88, 1.08)[index]
    # s, radius, half-thickness: thick roots and blunt carved edges.
    profile = [(.028, .0044, .0029), (.038, .017*height, .0018),
               (.061, .020*height, .0013), (.073, .016*height, .0017),
               (.104, .014*height, .0014), (.124, .0044, .0029)]
    mesh = bmesh.new()
    rings = [[mesh.verts.new(gs.at(s+shift) + outward*r + across*t*side)
              for s,r,t in profile] for side in (-1,1)]
    mesh.faces.new(rings[0])
    mesh.faces.new(list(reversed(rings[1])))
    for i in range(len(profile)):
        j=(i+1)%len(profile)
        mesh.faces.new((rings[0][i],rings[1][i],rings[1][j],rings[0][j]))
    bmesh.ops.recalc_face_normals(mesh, faces=mesh.faces)
    return gs.mesh_object(name,mesh,material,smooth=False)
