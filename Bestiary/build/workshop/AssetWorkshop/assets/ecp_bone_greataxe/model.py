"""1.5 metre bone greataxe: articulated vertebral haft and a bearded scapula blade."""
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
for folder in ('ecp_spine_greataxe', 'ecp_skel_arsenal'):
    sys.path.insert(0, os.path.join(HERE, '..', folder))
sys.path.insert(0, HERE)

import bpy
from mathutils import Matrix, Vector
import haft
from workshop import scene

TEXTURE_SIZE = 256
CATEGORY = 'weapon.axe_2h'
NORMAL_MAP = False
AO_STRENGTH = 0.2


def spine_point(z):
    t = max(0, min(1, (z - .065) / 1.102))
    return Vector((.040 * math.sin(t * math.tau), .012 * math.sin(t * math.pi), z))


def spine_axis(z):
    return (spine_point(z + .002) - spine_point(z - .002)).normalized()


def build():
    haft.build(spine_point)
    # Keep the user's approved head geometry and painted texture exactly as authored.
    with bpy.data.libraries.load(os.path.join(HERE, 'approved_head.blend')) as (src, dst):
        dst.objects = ['Approved_axe_head']
    head = dst.objects[0]
    bpy.context.collection.objects.link(head)
    head.data.uv_layers.active.name = 'ApprovedUV'
    head.data.uv_layers.new(name='UVMap')
    head.data.uv_layers.active_index = 1
    head.data.uv_layers['UVMap'].active_render = True
    for mat in head.data.materials:
        uv = mat.node_tree.nodes.new('ShaderNodeUVMap')
        uv.uv_map = 'ApprovedUV'
        for node in list(mat.node_tree.nodes):
            if node.type == 'TEX_IMAGE':
                mat.node_tree.links.new(uv.outputs['UV'], node.inputs['Vector'])
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for obj in meshes:
        if not obj.data.uv_layers:
            obj.data.uv_layers.new(name='UVMap')
    # Bake component placement before normalising the overall height.
    bpy.context.view_layer.update()
    for obj in meshes:
        obj.data.transform(obj.matrix_world)
        obj.matrix_world = Matrix.Identity(4)
        obj.data.update()
    bpy.context.view_layer.update()
    low, high = scene.bounds(meshes)
    factor = 1.5 / (high.z - low.z)
    # Grounded origin; all dimensions are baked in metres, no object scaling.
    for obj in meshes:
        for v in obj.data.vertices:
            v.co.z -= low.z
            v.co *= factor
        obj.data.update()
    bpy.context.view_layer.update()
    low, high = scene.bounds(meshes)
    assert abs(high.z - low.z - 1.5) < 1e-5
    # FBX tangents require triangles/quads; preserve the authored shading.
    import bmesh
    for obj in meshes:
        mesh = bmesh.new()
        mesh.from_mesh(obj.data)
        bmesh.ops.triangulate(mesh, faces=list(mesh.faces))
        mesh.to_mesh(obj.data)
        mesh.free()
