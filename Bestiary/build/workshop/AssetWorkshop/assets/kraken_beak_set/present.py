"""Build both assets and render previews, icons and a packed review scene."""
import os,sys
import bpy
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__)); ASSETS=os.path.dirname(HERE)
sys.path.insert(0,os.path.join(os.path.dirname(ASSETS),'blender'))
from workshop import pipeline,scene,materials
OUT=os.path.join(HERE,'out'); os.makedirs(OUT,exist_ok=True)
NAMES=('ecp_kraken_beak','ecp_kraken_beak_shield')

def viewport(center,distance):
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=='VIEW_3D':
                s=area.spaces.active; s.shading.type='MATERIAL'; s.overlay.show_overlays=False
                s.region_3d.view_rotation=Vector((.35,-1,.3)).to_track_quat('Z','Y')
                s.region_3d.view_location=Vector(center); s.region_3d.view_distance=distance
                s.region_3d.view_perspective='ORTHO'; s.clip_start=.001

def setup(target,scale):
    s=bpy.context.scene; s.render.engine='BLENDER_EEVEE'; s.view_settings.view_transform='Standard'
    s.render.resolution_percentage=100; s.render.image_settings.file_format='PNG'; s.render.image_settings.color_mode='RGBA'
    world=bpy.data.worlds.new('Soft studio'); world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.12,.15,.18,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65; s.world=world
    for name,position,power,size in [('Key',(-2,-3,4),380,3),('Fill',(3,-1,2),230,3),('Rim',(-1,2,3),320,2)]:
        data=bpy.data.lights.new(name,'AREA'); data.energy=power; data.size=size
        obj=bpy.data.objects.new(name,data); s.collection.objects.link(obj); obj.location=position
        obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('Camera'); data.type='ORTHO'; data.ortho_scale=scale; data.clip_start=.001
    obj=bpy.data.objects.new('Camera',data); s.collection.objects.link(obj)
    obj.location=Vector(target)+Vector((.7,-2,.6))
    obj.rotation_euler=(Vector(target)-obj.location).to_track_quat('-Z','Y').to_euler(); s.camera=obj
    return s

for name in NAMES:
    if '--shield-only' in sys.argv and 'shield' not in name:
        continue
    folder=os.path.join(ASSETS,name); pipeline.run(folder,render_preview=False)
    obj=bpy.data.objects[name]
    for node in obj.data.materials[0].node_tree.nodes:
        if node.type=='TEX_IMAGE': node.interpolation='Closest'
    materials.principled(obj.data.materials[0]).inputs['Roughness'].default_value=.72
    for other in bpy.context.scene.objects:
        if other.name.startswith('col_'): other.hide_set(True); other.hide_render=True
    target=(0,-.05,.52 if 'shield' in name else .215); scale=1.28 if 'shield' in name else .75
    viewport(target,scale*1.2); scene.select_only([obj]); bpy.ops.file.pack_all()
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(folder,'out',name+'.blend'))
    s=setup(target,scale); s.render.film_transparent=True
    for size,file in [(768,'preview.png'),(128,name+'_icon.png')]:
        s.render.resolution_x=size; s.render.resolution_y=size
        s.render.filepath=os.path.join(folder,'out',file); bpy.ops.render.render(write_still=True)
    if 'shield' in name:
        s.camera.location=(.7,2,.85); s.camera.rotation_euler=(Vector(target)-s.camera.location).to_track_quat('-Z','Y').to_euler()
        s.render.resolution_x=768; s.render.resolution_y=768; s.render.filepath=os.path.join(folder,'out','rear.png')
        bpy.ops.render.render(write_still=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
s=setup((0,0,.51),1.75); models=[]
for name,x in zip(NAMES,(-.55,.32)):
    with bpy.data.libraries.load(os.path.join(ASSETS,name,'out',name+'.blend'),link=False) as (src,dst): dst.objects=[name]
    obj=dst.objects[0]; s.collection.objects.link(obj); obj.location.x=x
    if 'shield' not in name: obj.location.z=.40; obj.location.y=-.10
    models.append(obj)
s.render.resolution_x=1300; s.render.resolution_y=1000
s.render.filepath=os.path.join(OUT,'kraken_beak_pair.png')
scene.select_only(models); viewport((0,0,.51),2.0); bpy.ops.file.pack_all()
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Kraken_Beak_Review.blend'))
