"""Build/export the axe, then render a local comparison with the vanilla Skeleton."""
import os
import sys
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, '..', '..', 'blender'))
import bpy
from mathutils import Vector
from workshop import pipeline, prefab, preview, scene

pipeline.run(HERE, render_preview=False)
OUT = os.path.join(HERE, 'out')
axe = bpy.data.objects['ecp_bone_greataxe']
mat = axe.data.materials[0]
for node in mat.node_tree.nodes:
    if node.type == 'TEX_IMAGE':
        node.interpolation = 'Closest'
    if node.type == 'BSDF_PRINCIPLED':
        node.inputs['Roughness'].default_value = .76
for image in bpy.data.images:
    if image.name.startswith('ecp_bone_greataxe'):
        image.pack()
bpy.context.scene.unit_settings.system = 'METRIC'
sys.path.insert(0, HERE)
from open_model import configure
configure()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'ecp_bone_greataxe.blend'))
preview.render_sheet(axe, OUT)
# Remove the generic scale dummy and stage meshes before the reference comparison.
for obj in list(bpy.data.objects):
    if obj.name.startswith('preview_') and obj.type == 'MESH' and obj != axe:
        bpy.data.objects.remove(obj, do_unlink=True)
skeleton = prefab.load('Characters/Skeleton/Skeleton.prefab', root_name='Vanilla_Skeleton_reference')
skeleton.location.x = -1.05
bpy.context.view_layer.update()
preview._stage(axe)
camera = preview._camera()
bpy.context.scene.render.resolution_x = 1400
bpy.context.scene.render.resolution_y = 1100
preview._render(camera, [axe] + prefab.meshes(skeleton), Vector((.15, -1, .10)), True,
                os.path.join(OUT, 'skeleton_comparison.png'))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'skeleton_comparison.blend'))
print('FINISHED: 1.500 m bone greataxe and vanilla reference comparison')
