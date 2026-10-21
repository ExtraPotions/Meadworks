"""Build the editable scene, baked FBXs, icons and inspection renders."""
from pathlib import Path
import sys, json, math
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
ASSETS=HERE.parent
sys.path.insert(0,str(HERE.parents[1]/'blender'))
sys.path.insert(0,str(HERE))
from workshop import scene, pipeline, materials
import design
OUT=HERE/'out'; OUT.mkdir(exist_ok=True)
NAMES=('ecp_bone_crossbow','ecp_bone_crossbow_unloaded','ecp_bone_quarrel')

def stage():
    s=bpy.context.scene; s.render.engine='BLENDER_EEVEE'; s.eevee.taa_render_samples=64
    s.render.resolution_percentage=100; s.render.image_settings.file_format='PNG'
    s.render.image_settings.color_mode='RGBA'; s.view_settings.view_transform='Standard'; s.view_settings.exposure=-.45
    w=bpy.data.worlds.new('slate studio'); w.use_nodes=True; s.world=w
    w.node_tree.nodes['Background'].inputs['Color'].default_value=(.035,.047,.053,1)
    w.node_tree.nodes['Background'].inputs['Strength'].default_value=.5
    for name,loc,energy,size,color in [('warm softbox',(-1,-2,3),210,3,(1,.91,.78)),
        ('cool fill',(2,0,2),145,3,(.78,.86,1)),('edge light',(-1,2,1.5),190,2,(1,.92,.78))]:
        d=bpy.data.lights.new(name,'AREA'); d.energy=energy; d.shape='DISK'; d.size=size; d.color=color
        o=bpy.data.objects.new(name,d); s.collection.objects.link(o); o.location=loc
        o.rotation_euler=(Vector((0,-.15,0))-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.cameras.new('Review Camera'); d.type='ORTHO'; d.clip_start=.001
    o=bpy.data.objects.new('Review Camera',d); s.collection.objects.link(o); s.camera=o
    return s

def view(s,path,direction=(1,-1.4,2.3),target=(0,-.16,0),scale=1.50,width=1300,height=1000,transparent=False):
    s.camera.location=Vector(target)+Vector(direction)*2
    s.camera.rotation_euler=(Vector(target)-s.camera.location).to_track_quat('-Z','Y').to_euler()
    s.camera.data.ortho_scale=scale; s.render.resolution_x=width; s.render.resolution_y=height
    s.render.film_transparent=transparent; s.render.filepath=str(path)
    bpy.ops.render.render(write_still=True)

def viewport():
    for scr in bpy.data.screens:
        for area in scr.areas:
            if area.type=='VIEW_3D':
                a=area.spaces.active; a.shading.type='MATERIAL'; a.overlay.show_overlays=False
                a.region_3d.view_rotation=Vector((1,-1.4,2.3)).to_track_quat('Z','Y')
                a.region_3d.view_location=(0,-.16,0); a.region_3d.view_distance=1.5
                a.region_3d.view_perspective='ORTHO'; a.clip_start=.001

def original_scene():
    scene.clear(); m=design.palette(); design.body(m)
    body=[o for o in sobjects() if o.type=='MESH']
    string=design.string(m,True)
    string.shape_key_add(name='Drawn'); released=string.shape_key_add(name='Released')
    # Rebuild matching topology and use its vertices as the released shape.
    spare=design.string(m,False)
    for dst,src in zip(released.data,spare.data.vertices): dst.co=src.co
    bpy.data.objects.remove(spare,do_unlink=True)
    before=set(sobjects()); design.bolt(m); parts=[o for o in sobjects() if o not in before]
    bolt=scene.join(parts,'Loaded bone quarrel'); bolt.location=(0,-.075,.058)
    # A repeatable fire / draw / seat demonstration; no gameplay simulation is claimed.
    for frame,val in [(1,0),(18,0),(20,1),(40,1),(75,0),(90,0)]:
        released.value=val; released.keyframe_insert(data_path='value',frame=frame)
    for frame,loc,scale in [(1,(0,-.075,.058),1),(18,(0,-.075,.058),1),
        (24,(0,-2.5,.058),1),(25,(0,-2.5,.058),0),(70,(0,-.075,.25),0),
        (71,(0,-.075,.25),1),(83,(0,-.075,.058),1),(90,(0,-.075,.058),1)]:
        bolt.location=loc; bolt.scale=(scale,)*3
        bolt.keyframe_insert(data_path='location',frame=frame); bolt.keyframe_insert(data_path='scale',frame=frame)
    s=stage(); s.render.fps=30; s.frame_start=1; s.frame_end=90; s.frame_set(1)
    for frame,name in [(18,'FIRE - release projectile'),(40,'DRAW'),(71,'SEAT BOLT'),(83,'READY')]:
        s.timeline_markers.new(name,frame=frame)
    for name,loc in [('Grip',(0,.09,-.035)),('SupportHand',(0,-.29,-.055)),
        ('BoltSeat',(0,-.075,.058)),('Muzzle',(0,-.565,.058))]:
        o=bpy.data.objects.new(name,None); s.collection.objects.link(o); o.location=loc; o.empty_display_size=.025
    viewport(); scene.select_only(body+[string]); bpy.ops.file.pack_all()
    view(s,OUT/'source_preview.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Gravebranch_editable.blend'))

def sobjects(): return list(bpy.context.scene.objects)

def baked_assets():
    for name in NAMES:
        pipeline.run(str(ASSETS/name),render_preview=False)
        obj=bpy.data.objects[name]
        for n in obj.data.materials[0].node_tree.nodes:
            if n.type=='TEX_IMAGE': n.interpolation='Closest'
        materials.principled(obj.data.materials[0]).inputs['Roughness'].default_value=.88
        for o in sobjects():
            if o.name.startswith('col_'): o.hide_set(True); o.hide_render=True
        viewport(); scene.select_only([obj]); bpy.ops.file.pack_all()
        bpy.ops.wm.save_as_mainfile(filepath=str(ASSETS/name/'out'/(name+'.blend')))
        s=stage()
        is_bolt='quarrel' in name
        view(s,ASSETS/name/'out'/(name+'_icon.png'),scale=.62 if is_bolt else 1.35,width=128,height=128,transparent=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for name in (NAMES[0],NAMES[2]):
        with bpy.data.libraries.load(str(ASSETS/name/'out'/(name+'.blend')),link=False) as (src,dst): dst.objects=[name]
        obj=dst.objects[0]; bpy.context.collection.objects.link(obj)
        if name==NAMES[2]:
            obj.location=(0,-.075,.058)
            spare=obj.copy(); spare.data=obj.data.copy(); bpy.context.collection.objects.link(spare)
            spare.name='matching quarrel display'; spare.location=(.67,-.025,0)
    s=stage(); viewport(); bpy.ops.file.pack_all()
    view(s,OUT/'hero.png',target=(.06,-.15,0),scale=1.62,width=1600,height=1200)
    view(s,OUT/'top.png',direction=(0,0,3),target=(.07,-.15,0),scale=1.65)
    spare.hide_render=True
    view(s,OUT/'side.png',direction=(3,.1,.35),scale=1.25)
    view(s,OUT/'underside.png',direction=(1,-1.3,-2),scale=1.4)
    view(s,OUT/'detail.png',direction=(.6,-.6,2),target=(0,-.36,.015),scale=.84)
    spare.hide_render=False
    view(s,OUT/'hero.png',target=(.06,-.15,0),scale=1.62,width=1600,height=1200)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Gravebranch_review.blend'))
    report={name:json.loads((ASSETS/name/'out'/(name+'.json')).read_text()) for name in NAMES}
    (OUT/'manifest.json').write_text(json.dumps(report,indent=2))

original_scene()
if '--draft' not in sys.argv: baked_assets()
