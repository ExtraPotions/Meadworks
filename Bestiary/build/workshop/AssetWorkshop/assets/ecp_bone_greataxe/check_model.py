"""Verify the exported mesh keeps the approved head and the established dimensions."""
import json
import os
import bpy
from mathutils.kdtree import KDTree

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, 'out')
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT, 'ecp_bone_greataxe.blend'))
axe = bpy.data.objects['ecp_bone_greataxe']
with bpy.data.libraries.load(os.path.join(HERE, 'approved_head.blend')) as (src, dst):
    dst.objects = ['Approved_axe_head']
head = dst.objects[0]
tree = KDTree(len(axe.data.vertices))
for v in axe.data.vertices:
    tree.insert(v.co, v.index)
tree.balance()
error = max(tree.find(head.matrix_world @ v.co)[2] for v in head.data.vertices)
height = max(v.co.z for v in axe.data.vertices) - min(v.co.z for v in axe.data.vertices)
axe.data.calc_loop_triangles()
head.data.calc_loop_triangles()
result = dict(height_metres=height, approved_head_max_vertex_error_metres=error,
              total_triangles=len(axe.data.loop_triangles),
              head_triangles=len(head.data.loop_triangles),
              new_handle_triangles=len(axe.data.loop_triangles) - len(head.data.loop_triangles))
assert abs(height - 1.5) < 1e-5, result
assert error < 1e-5, result
assert result['new_handle_triangles'] < 1500, result
assert all(v.co.x == v.co.x for v in axe.data.vertices)
with open(os.path.join(OUT, 'model_validation.json'), 'w') as f:
    json.dump(result, f, indent=2)
print(json.dumps(result, indent=2))
