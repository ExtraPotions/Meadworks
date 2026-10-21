"""Original Valheim-inspired item-hint icons, rendered from native Blender geometry.
Run Blender --background --factory-startup --python PackPanel/artwork/slot_icons.py.
Writes only icon_*.png to PackPanel assets; masters and comparison stay in artwork/slot-icons.
"""
import os,sys,math,json
import bpy
from mathutils import Vector
HERE=os.path.dirname(os.path.abspath(__file__))
ROOT=os.path.abspath(os.path.join(HERE,'..','..'))
sys.path.insert(0,os.path.join(ROOT,'..','ValheimAssets','Tools','Blender'))   # the asset workshop beside this repo
from workshop import shapes,materials,scene,paths
OUT=os.path.join(HERE,'slot-icons'); os.makedirs(OUT,exist_ok=True)
DEST=os.path.join(HERE,'..','PackPanel','assets')
NAMES=('head','chest','legs','back','backpack','utility','food','mead','ammo','purse','key','tacklebox','tackle')

def mat(name,c):
    m=materials.flat(name,c,roughness=.78); tree=m.node_tree
    tex=tree.nodes.new('ShaderNodeTexNoise'); tex.inputs['Scale'].default_value=22; tex.inputs['Detail'].default_value=1
    ramp=tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=(*(v*.65 for v in c),1); ramp.color_ramp.elements[0].position=.25
    ramp.color_ramp.elements[1].color=(*(min(1,v*1.3) for v in c),1); ramp.color_ramp.elements[1].position=.75
    tree.links.new(tex.outputs['Fac'],ramp.inputs[0]); tree.links.new(ramp.outputs[0],materials.principled(m).inputs['Base Color'])
    return m

def palette():
    return {k:mat(k,c) for k,c in {'iron':(.18,.205,.21),'dark':(.028,.025,.023),'leather':(.20,.075,.027),
        'worn':(.33,.18,.072),'gold':(.51,.285,.055),'bone':(.59,.48,.29),'blue':(.043,.10,.18),
        'meat':(.25,.065,.018),'red':(.26,.025,.015),'wood':(.19,.105,.042),'feather':(.39,.38,.29)}.items()}

def rod(name,a,b,r,m):
    a,b=Vector(a),Vector(b); obj=shapes.cylinder(name,r,(b-a).length,(a+b)/2,material=m,vertices=10)
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return obj

def loop(name,c,rx,rz,r,m,n=24):
    x,y,z=c
    points=[(x+rx*math.cos(i*math.tau/n),y,z+rz*math.sin(i*math.tau/n)) for i in range(n+1)]
    for a,b in zip(points,points[1:]): rod(name,a,b,r,m)

def slab(name,outline,y,depth,m):
    n=len(outline); verts=[(x,yy,z) for yy in (y,y+depth) for x,z in outline]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); data.materials.append(m)
    bevel=obj.modifiers.new('soft worn edge','BEVEL'); bevel.width=.012; bevel.segments=1
    return obj

def buckle(x,y,z,m):
    for a,b in [((-.065,-.055),(.065,-.055)),((.065,-.055),(.065,.055)),((.065,.055),(-.065,.055)),((-.065,.055),(-.065,-.055))]:
        rod('buckle',(x+a[0],y,z+a[1]),(x+b[0],y,z+b[1]),.012,m)
    rod('pin',(x,y-.006,z-.05),(x,y-.006,z+.032),.007,m)

def append_asset(asset):
    file=os.path.join(paths.asset_dir(asset),'out',asset+'.blend')
    with bpy.data.libraries.load(file,link=False) as (src,dst): dst.objects=[asset]
    obj=dst.objects[0]; bpy.context.collection.objects.link(obj)
    # PackPanel wearable models display toward +Y; icon camera looks from -Y.
    if 'backpack' in asset: obj.rotation_euler.z=math.pi
    return obj

def build(name):
    p=palette()
    if name=='backpack': append_asset('packpanel_trollhide_backpack'); return
    if name=='tacklebox': append_asset('packpanel_driftwood_tacklebox'); return
    if name=='head':
        verts=[]; faces=[]; n=20
        for j in range(7):
            a=(.05+j/6*(math.pi/2-.05))
            for i in range(n):
                t=i*math.tau/n; verts.append((.30*math.sin(a)*math.cos(t),.26*math.sin(a)*math.sin(t),.12+.34*math.cos(a)))
        for j in range(6):
            for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        data=bpy.data.meshes.new('helmet'); data.from_pydata(verts,[],faces); data.materials.append(p['iron'])
        obj=bpy.data.objects.new('helmet',data); bpy.context.collection.objects.link(obj)
        shapes.cylinder('helmet rim',.30,.055,(0,0,.12),material=p['dark'],vertices=20)
        slab('nose guard',[(-.025,.14),(.025,.14),(.035,-.09),(0,-.13),(-.035,-.09)],-.283,.025,p['iron'])
        for side in (-1,1):
            slab('cheek guard',[(side*.20,.14),(side*.29,.12),(side*.26,-.16),(side*.17,-.12)],-.15,.035,p['iron'])
        for x in (-.21,-.12,.12,.21): shapes.sphere('rivet',.013,(x,-.245,.14),material=p['gold'],segments=8,rings=4)
    elif name=='chest':
        outline=[(-.12,.40),(-.30,.33),(-.40,.11),(-.26,.065),(-.21,.18),(-.22,-.28),(.22,-.28),(.21,.18),(.26,.065),(.40,.11),(.30,.33),(.12,.40),(.07,.30),(-.07,.30)]
        slab('leather tunic',outline,-.10,.17,p['leather'])
        for side in (-1,1):
            slab('shoulder reinforcement',[(side*.13,.37),(side*.30,.32),(side*.34,.22),(side*.17,.21)],-.125,.022,p['worn'])
        shapes.box('belt',(.45,.035,.060),(0,-.12,-.12),material=p['dark'],bevel=.01); buckle(0,-.15,-.12,p['gold'])
        for z in (.03,.095,.16,.225): rod('stitched front',(-.018,-.12,z),(.018,-.12,z+.02),.006,p['bone'])
    elif name=='legs':
        slab('leather trousers',[(-.22,.35),(.22,.35),(.25,-.29),(.065,-.29),(0,.06),(-.065,-.29),(-.25,-.29)],-.06,.15,p['leather'])
        shapes.box('waist band',(.45,.035,.06),(0,-.08,.30),material=p['dark'],bevel=.01)
        buckle(0,-.105,.30,p['iron'])
        for side in (-1,1):
            shapes.box('worn boots',(.19,.23,.16),(side*.16,-.005,-.29),material=p['dark'],bevel=.028)
            for z in (-.19,-.24): rod('boot ties',(side*.16-.07,-.133,z),(side*.16+.07,-.133,z),.009,p['worn'])
    elif name=='back':
        verts=[]; faces=[]; n=12
        for j in range(7):
            t=j/6
            for i in range(n+1):
                u=i/n*2-1; verts.append((u*(.11+.22*t),-.04-.038*math.cos(u*math.pi*3)*t,.37-.78*t+.025*math.cos(i*2)*t**5))
        for j in range(6):
            for i in range(n): k=j*(n+1)+i; faces.append((k,k+1,k+n+2,k+n+1))
        data=bpy.data.meshes.new('draped hide'); data.from_pydata(verts,[],faces); data.materials.append(p['blue'])
        obj=bpy.data.objects.new('trollhide cape',data); bpy.context.collection.objects.link(obj)
        mod=obj.modifiers.new('hide thickness','SOLIDIFY'); mod.thickness=.01
        loop('collar',(0,-.01,.36),.12,.055,.018,p['dark']); shapes.sphere('clasp',.024,(0,-.08,.31),material=p['gold'],segments=10,rings=5)
    elif name=='utility':
        loop('leather belt',(0,0,0),.34,.21,.047,p['leather'])
        buckle(0,-.055,-.19,p['gold'])
        for x in (-.12,.12): shapes.sphere('belt stud',.015,(x,-.05,-.178),material=p['gold'],segments=8,rings=4)
        rod('belt keeper',(.22,-.02,-.17),(.25,-.02,-.11),.022,p['dark'])
    elif name=='food':
        rod('roast bone',(-.21,0,-.24),(.02,0,.12),.044,p['bone'])
        for x in (-.23,-.18): shapes.sphere('bone end',.055,(x,0,-.25),material=p['bone'],segments=10,rings=6)
        obj=shapes.sphere('cooked meat',.23,(.06,0,.10),material=p['meat'],segments=14,rings=10); obj.scale=(.85,.70,1.22); obj.rotation_euler.y=-.45
        for i in range(3): rod('roasted scoring',(-.09+i*.07,-.158,.10),(-.01+i*.07,-.16,.17),.008,p['worn'])
    elif name=='mead':
        verts=[]; faces=[]; n=16
        rings=[(-.16,0,-.34,.009),(-.09,0,-.23,.044),(.005,0,-.07,.09),(.06,0,.12,.14),(.04,0,.28,.16)]
        for x,y,z,r in rings:
            for i in range(n): a=i*math.tau/n; verts.append((x+r*math.cos(a),y+r*math.sin(a),z))
        for j in range(4):
            for i in range(n): faces.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
        data=bpy.data.meshes.new('horn'); data.from_pydata(verts,[],faces); data.materials.append(p['bone'])
        obj=bpy.data.objects.new('drinking horn',data); bpy.context.collection.objects.link(obj)
        shapes.cylinder('dark mead',.147,.013,(.04,0,.265),material=p['dark'],vertices=16)
        bpy.ops.mesh.primitive_torus_add(major_radius=.16,minor_radius=.013,major_segments=20,minor_segments=6,location=(.04,0,.28)); bpy.context.object.data.materials.append(p['gold'])
        shapes.cylinder('bronze horn band',.098,.045,(.012,0,-.02),material=p['gold'],vertices=16)
    elif name=='ammo':
        for off in (-.065,.065):
            rod('wooden arrow',(off-.20,0,-.32),(off+.20,0,.32),.014,p['wood'])
            slab('iron arrowhead',[(off+.15,.26),(off+.20,.43),(off+.31,.27),(off+.24,.27)],-.015,.035,p['iron'])
            for j in range(2):
                x=off-.19+j*.05; z=-.30+j*.08
                slab('feather fletching',[(x,z),(x-.09,z-.025),(x-.09,z+.10),(x+.045,z+.10)],-.012,.025,p['feather'])
    elif name=='purse':
        for x,y,z,n in [(-.15,0,0,3),(.15,.07,0,5),(.0,-.18,0,1)]:
            for i in range(n):
                shapes.cylinder('gold coin',.14,.027,(x,y,z+i*.027),material=p['gold'],vertices=16,bevel=.004)
            # Small raised diamond mint stamp on each stack's top coin.
            zz=z+(n-1)*.027+.016
            for a,b in [((-.06,0),(0,.07)),((0,.07),(.06,0)),((.06,0),(0,-.07)),((0,-.07),(-.06,0))]:
                rod('coin stamp',(x+a[0],y+a[1],zz),(x+b[0],y+b[1],zz),.006,p['worn'])
    elif name=='key':
        loop('key ring',(0,.015,.25),.14,.14,.016,p['iron'])
        for x,z,m in [(-.08,.12,p['iron']),(.10,.07,p['gold'])]:
            loop('key bow',(x,-.015,z),.065,.062,.013,m)
            rod('key shaft',(x,-.015,z-.05),(x,-.015,z-.37),.016,m)
            for zz in (z-.28,z-.35): shapes.box('key tooth',(.074,.027,.034),(x+.028,-.015,zz),material=m,bevel=.004)
    elif name=='tackle':
        pts=[(.13+.095*math.cos(a),0,-.12+.095*math.sin(a)) for a in [math.pi+i*math.pi/18 for i in range(19)]]
        rod('hook shank',(.035,0,-.12),(.035,0,.20),.013,p['iron'])
        for a,b in zip(pts,pts[1:]): rod('fishing hook',a,b,.013,p['iron'])
        rod('hook barb',(.225,0,-.12),(.20,0,-.15),.009,p['iron'])
        shapes.sphere('painted fishing float',.095,(-.12,0,.16),material=p['red'],segments=12,rings=8)
        shapes.cylinder('float cork',.064,.08,(-.12,0,.235),material=p['bone'],vertices=12)
        rod('float stem',(-.12,0,.25),(-.12,0,.34),.012,p['wood'])

def render(name):
    bpy.ops.wm.read_factory_settings(use_empty=True); build(name)
    objs=list(scene.meshes()); scene.select_only(objs); bpy.context.view_layer.update()
    low,high=scene.bounds(objs); center=(low+high)/2
    s=bpy.context.scene; s.render.engine='BLENDER_EEVEE'; s.view_settings.view_transform='Standard'
    s.render.film_transparent=True; s.render.image_settings.file_format='PNG'; s.render.image_settings.color_mode='RGBA'
    world=bpy.data.worlds.new('studio'); world.use_nodes=True; world.node_tree.nodes['Background'].inputs['Strength'].default_value=.65; s.world=world
    for title,position,power,size in [('key',(-3,-4,5),450,4),('fill',(3,-1,2),150,3)]:
        data=bpy.data.lights.new(title,'AREA'); data.energy=power; data.size=size
        obj=bpy.data.objects.new(title,data); s.collection.objects.link(obj); obj.location=center+Vector(position)
        obj.rotation_euler=(center-obj.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new('icon camera'); cam=bpy.data.objects.new('icon camera',data); s.collection.objects.link(cam)
    direction=Vector((.7,-3,1.4 if name=='purse' else .8)); cam.location=center+direction
    cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler(); data.type='ORTHO'; data.clip_start=.001
    rotation=cam.rotation_euler.to_matrix().transposed()
    projected=[rotation@(o.matrix_world@Vector(c)-center) for o in objs for c in o.bound_box]
    data.ortho_scale=max(max(v.x for v in projected)-min(v.x for v in projected),max(v.y for v in projected)-min(v.y for v in projected))*1.20
    s.camera=cam; s.render.resolution_percentage=100
    for size,path in [(256,os.path.join(OUT,'icon_'+name+'.png')),(64,os.path.join(DEST,'icon_'+name+'.png'))]:
        s.render.resolution_x=size; s.render.resolution_y=size; s.render.filepath=path; bpy.ops.render.render(write_still=True)
    if name=='backpack': bpy.ops.file.pack_all(); bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'lighting_reference.blend'))
    print('ICON '+name,flush=True)

def sheet():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    s=bpy.context.scene; s.render.engine='BLENDER_EEVEE'; s.view_settings.view_transform='Standard'
    s.render.resolution_x=1200; s.render.resolution_y=820; s.render.resolution_percentage=100
    world=bpy.data.worlds.new('brown'); world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.025,.019,.012,1); s.world=world
    for i,name in enumerate(NAMES):
        x=(i%5-2)*2; z=1.9-(i//5)*2
        m=materials.flat(name,(1,1,1)); nt=m.node_tree; nt.nodes.clear()
        tex=nt.nodes.new('ShaderNodeTexImage'); tex.image=bpy.data.images.load(os.path.join(DEST,'icon_'+name+'.png')); tex.interpolation='Closest'
        emit=nt.nodes.new('ShaderNodeEmission'); nt.links.new(tex.outputs['Color'],emit.inputs['Color'])
        transparent=nt.nodes.new('ShaderNodeBsdfTransparent'); mix=nt.nodes.new('ShaderNodeMixShader'); nt.links.new(tex.outputs['Alpha'],mix.inputs[0]); nt.links.new(transparent.outputs[0],mix.inputs[1]); nt.links.new(emit.outputs[0],mix.inputs[2])
        output=nt.nodes.new('ShaderNodeOutputMaterial'); nt.links.new(mix.outputs[0],output.inputs[0])
        bpy.ops.mesh.primitive_plane_add(size=1.65,location=(x,0,z),rotation=(math.pi/2,0,0)); bpy.context.object.data.materials.append(m)
        data=bpy.data.curves.new(name,'FONT'); data.body=name.upper(); data.align_x='CENTER'; data.size=.18
        label=bpy.data.objects.new(name,data); s.collection.objects.link(label); label.location=(x,-.01,z-1); label.rotation_euler=(math.pi/2,0,0)
        textmat=materials.flat('text',(.65,.56,.40)); bs=materials.principled(textmat); bs.inputs['Emission Color'].default_value=(.65,.56,.40,1); bs.inputs['Emission Strength'].default_value=1; data.materials.append(textmat)
    camdata=bpy.data.cameras.new('comparison'); cam=bpy.data.objects.new('comparison',camdata); s.collection.objects.link(cam)
    cam.location=(0,-10,-.25); cam.rotation_euler=(math.pi/2,0,0); camdata.type='ORTHO'; camdata.ortho_scale=10.3; s.camera=cam
    s.render.filepath=os.path.join(OUT,'comparison.png'); bpy.ops.render.render(write_still=True)

if __name__=='__main__':
    if '--sheet-only' not in sys.argv:
        for name in NAMES: render(name)
    sheet()
