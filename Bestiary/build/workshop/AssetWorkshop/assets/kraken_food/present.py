"""Build standalone food assets, transparent icons, and a packed Blender review."""
import os
import sys
import bpy
from mathutils import Vector

HERE=os.path.dirname(os.path.abspath(__file__))
ASSETS=os.path.dirname(HERE)
sys.path.insert(0,os.path.join(os.path.dirname(ASSETS),'blender'))
from workshop import pipeline,scene,materials
NAMES=('ecp_kraken_meat','ecp_kraken_meat_cooked')
OUT=os.path.join(HERE,'out')
os.makedirs(OUT,exist_ok=True)

def viewport(center,distance):
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                space=area.spaces.active
                space.shading.type='MATERIAL'
                space.overlay.show_overlays=False
                space.region_3d.view_rotation=Vector((-.9,-1,.95)).to_track_quat('Z','Y')
                space.region_3d.view_location=Vector(center)
                space.region_3d.view_distance=distance
                space.region_3d.view_perspective='ORTHO'
                space.clip_start=.001

def setup():
    s=bpy.context.scene
    s.render.engine='BLENDER_EEVEE'
    s.render.resolution_percentage=100
    s.view_settings.view_transform='Standard'
    s.render.image_settings.file_format='PNG'
    s.render.image_settings.color_mode='RGBA'
    world=bpy.data.worlds.new('Neutral lighting'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.18,.20,.24,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65
    s.world=world
    for name,pos,power,size in [('Soft key',(-.6,-.7,1),35,.9),('Fill',(.6,.3,.7),18,.7)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.size=size
        obj=bpy.data.objects.new(name,data); s.collection.objects.link(obj); obj.location=pos
        obj.rotation_euler=(Vector((0,0,.05))-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Camera'); data.type='ORTHO'; data.ortho_scale=.27; data.clip_start=.001
    cam=bpy.data.objects.new('Camera',data); s.collection.objects.link(cam)
    cam.location=(-.6,-.7,.65); cam.rotation_euler=(Vector((0,0,.048))-cam.location).to_track_quat('-Z','Y').to_euler()
    s.camera=cam
    return s

for name in NAMES:
    if '--cooked-only' in sys.argv and name != 'ecp_kraken_meat_cooked':
        continue
    folder=os.path.join(ASSETS,name)
    pipeline.run(folder,render_preview=False)
    obj=bpy.data.objects[name]
    mat=obj.data.materials[0]
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE': node.interpolation='Closest'
    materials.principled(mat).inputs['Roughness'].default_value=.8
    for other in bpy.context.scene.objects:
        if other.name.startswith('col_'): other.hide_set(True); other.hide_render=True
    scene.select_only([obj]); viewport((0,0,.05),.36)
    bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder,'out',name+'.blend'))
    s=setup(); s.render.film_transparent=True
    s.render.resolution_x=512; s.render.resolution_y=512
    s.render.filepath=os.path.join(folder,'out','preview.png'); bpy.ops.render.render(write_still=True)
    s.render.resolution_x=128; s.render.resolution_y=128
    s.render.filepath=os.path.join(folder,'out',name+'_icon.png'); bpy.ops.render.render(write_still=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s=setup(); models=[]
# Spread along camera's horizontal axis to keep both portions at equal depth.
right=Vector((.759,-.651,0))
for name,offset in zip(NAMES,(-.14,.14)):
    with bpy.data.libraries.load(os.path.join(ASSETS,name,'out',name+'.blend'),link=False) as (src,dst):
        dst.objects=[name]
    obj=dst.objects[0]; s.collection.objects.link(obj); obj.location=right*offset; models.append(obj)
s.camera.data.ortho_scale=.59
s.render.resolution_x=1200; s.render.resolution_y=700
s.render.film_transparent=False
s.render.filepath=os.path.join(OUT,'kraken_meat_pair.png')
scene.select_only(models); viewport((0,0,.05),.7)
bpy.ops.file.pack_all()
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Kraken_Meat_Review.blend'))
