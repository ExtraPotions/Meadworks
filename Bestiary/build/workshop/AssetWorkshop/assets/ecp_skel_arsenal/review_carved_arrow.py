"""Coloured carved-arrow review and close-up evidence; source dimensions retained."""
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Vector
import remake_review as review
review.scene.clear()
obj=review.load_item('ecp_skel_arrow')
obj.data.calc_loop_triangles()
assert len(obj.data.loop_triangles)<=350
assert len(obj.data.materials)==1
length=max(v.co.y for v in obj.data.vertices)-min(v.co.y for v in obj.data.vertices)
assert abs(length-1.169)<.00001
review.orient(obj,(0,-1,0),(0,0,1),True)
bpy.context.view_layer.update()
review.icons._light()
review.icons._camera(obj)
review.icons.SIZE=1000
review.icons._render(str(review.OUT/'carved_arrow_review.png'))
s=bpy.context.scene
cam=s.camera
original=cam.matrix_world.copy()
scale=cam.data.ortho_scale
for label,station in [('point',1.07),('fins',.075)]:
    target=obj.matrix_world@Vector((0,-station,0))
    cam.location=target+Vector((0,-2,0))
    cam.rotation_euler=(Vector((0,1,0))).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=.24
    review.icons._render(str(review.OUT/f'carved_arrow_{label}.png'))
cam.matrix_world=original
cam.data.ortho_scale=scale
s.render.film_transparent=False
s.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.085,.09,1)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            space=area.spaces.active
            space.shading.type='MATERIAL'
            space.shading.use_scene_world=True
            space.shading.use_scene_lights=True
            space.overlay.show_overlays=False
            space.region_3d.view_perspective='CAMERA'
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active=obj
bpy.ops.wm.save_as_mainfile(filepath=str(review.OUT/'carved_arrow_review.blend'))
print('VERIFIED',len(obj.data.loop_triangles),'triangles; length',length)
