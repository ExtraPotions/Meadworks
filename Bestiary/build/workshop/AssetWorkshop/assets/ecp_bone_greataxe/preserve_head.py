"""Preserve the approved head from the first finished model, including its painted UVs."""
import bpy
import bmesh
import os
HERE = os.path.dirname(os.path.abspath(__file__))
with bpy.data.libraries.load(os.path.join(HERE, 'original_held_source.blend')) as (src, dst):
    dst.objects = ['ecp_bone_greataxe']
obj = dst.objects[0]
mesh = bmesh.new()
mesh.from_mesh(obj.data)
todo = set(mesh.verts)
remove = []
while todo:
    seed = todo.pop()
    group = {seed}
    stack = [seed]
    while stack:
        for edge in stack.pop().link_edges:
            for v in edge.verts:
                if v in todo:
                    todo.remove(v)
                    group.add(v)
                    stack.append(v)
    keep = max(v.co.z for v in group) > 1.20 or (min(v.co.x for v in group) > .10 and max(v.co.z for v in group) > .60)
    if not keep:
        remove.extend(group)
bmesh.ops.delete(mesh, geom=remove, context='VERTS')
mesh.to_mesh(obj.data)
mesh.free()
obj.name = 'Approved_axe_head'
obj.parent = None
from mathutils import Matrix
obj.matrix_world = Matrix.Identity(4)
for image in bpy.data.images:
    if image.source == 'FILE':
        image.pack()
bpy.data.libraries.write(os.path.join(HERE, 'approved_head.blend'), {obj}, fake_user=True)
print('Preserved head:', len(obj.data.polygons), 'faces')
