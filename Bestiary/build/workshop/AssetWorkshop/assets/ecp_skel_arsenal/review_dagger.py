"""Show the extended dagger at actual scale with packed colour textures."""
import json
import sys
from pathlib import Path
import bpy
sys.path.insert(0, str(Path(__file__).resolve().parent))
import remake_review as review

review.scene.clear()
obj = review.load_item('ecp_skel_dagger')
obj.data.calc_loop_triangles()
assert len(obj.data.loop_triangles) == 518
length = max(v.co.y for v in obj.data.vertices) - min(v.co.y for v in obj.data.vertices)
assert abs(length - .4778) < .00001, length
review.orient(obj, (0, -1, 0), (0, 0, 1), True)
bpy.context.view_layer.update()
review.icons._light()
review.icons._camera(obj)
s = bpy.context.scene
s.render.engine = 'BLENDER_EEVEE'
s.render.resolution_x = s.render.resolution_y = 1000
s.render.resolution_percentage = 100
s.view_settings.view_transform = 'Standard'
s.world.node_tree.nodes['Background'].inputs[0].default_value = (.075, .085, .09, 1)
bpy.ops.object.select_all(action='DESELECT')
obj.select_set(True)
bpy.context.view_layer.objects.active = obj
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            space = area.spaces.active
            space.shading.type = 'MATERIAL'
            space.shading.use_scene_world = True
            space.shading.use_scene_lights = True
            space.overlay.show_overlays = False
            space.region_3d.view_perspective = 'CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=str(review.OUT / 'dagger_review.blend'))
print(json.dumps({'triangles':518, 'total_length_m':length, 'blade_length_m':.2438,
                  'hilt_length_m':.234, 'blade_scale':1.15, 'hilt_scale':1.3}))
