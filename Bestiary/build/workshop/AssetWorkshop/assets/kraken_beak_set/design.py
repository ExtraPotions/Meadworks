"""Original faceted kraken beak and a crafted shield. Metres; front -Y."""
import math
import bpy
import bmesh
from mathutils import Vector
from workshop import shapes,materials

def mesh(name,verts,faces,mats,indices=None):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    for mat in mats: data.materials.append(mat)
    if indices:
        for face,index in zip(data.polygons,indices): face.material_index=index
    # Consistent outward normals for baking and Unity backface culling.
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT'); obj.select_set(False)
    return obj

def mottled(name,dark,light):
    mat=materials.flat(name,dark)
    tree=mat.node_tree; noise=tree.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=24
    coord=tree.nodes.new('ShaderNodeTexCoord'); tree.links.new(coord.outputs['Object'],noise.inputs['Vector'])
    ramp=tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=(*dark,1); ramp.color_ramp.elements[1].color=(*light,1)
    tree.links.new(noise.outputs['Fac'],ramp.inputs[0]); tree.links.new(ramp.outputs[0],materials.principled(mat).inputs['Base Color'])
    return mat

def palette():
    return [mottled('dark aubergine horn',(.012,.008,.016),(.075,.034,.042)),
            mottled('worn amber cutting edges',(.15,.07,.024),(.43,.28,.115)),
            mottled('inner horn',(.035,.018,.02),(.13,.065,.055))]

def jaw(name,stations,mats,scale,offset,lower=False):
    # Add curved intermediate sections while retaining the broad hand-cut facets.
    original=stations; stations=[]
    for j in range(len(original)-1):
        p0=Vector(original[max(0,j-1)]); p1=Vector(original[j])
        p2=Vector(original[j+1]); p3=Vector(original[min(len(original)-1,j+2)])
        stations.append(tuple(p1))
        t=.5
        stations.append(tuple(.5*((2*p1)+(-p0+p2)*t+(2*p0-5*p1+4*p2-p3)*t*t+(-p0+3*p1-3*p2+p3)*t*t*t)))
    stations.append(original[-1])
    verts=[]; n=13
    for inner in (False,True):
        for y,w,z,h in stations:
            for i in range(n):
                a=math.pi*i/(n-1)
                x=w*math.cos(a); zz=z+h*math.sin(a)*(-1 if lower else 1)
                if inner: x*=.86; zz+=.012 if lower else -.012
                verts.append(Vector((x,y,zz))*scale+Vector(offset))
    faces=[]; ids=[]; layer=len(stations)*n
    for side in range(2):
        for j in range(len(stations)-1):
            for i in range(n-1):
                k=side*layer+j*n+i
                faces.append((k,k+1,k+n+1,k+n)); ids.append(2 if side else (1 if i<2 or i>=n-3 or j>=len(stations)-3 else 0))
    for j in range(len(stations)-1):
        for i in (0,n-1):
            k=j*n+i; faces.append((k,k+n,k+n+layer,k+layer)); ids.append(1)
    for j in (0,len(stations)-1):
        for i in range(n-1):
            k=j*n+i; faces.append((k,k+1,k+1+layer,k+layer)); ids.append(1)
    return mesh(name,verts,faces,mats,ids)

BEAK_SCALE=1.65

def beak(scale=BEAK_SCALE,offset=(0,0,0)):
    mats=palette()
    jaw('hooked upper beak',[(.10,.12,.085,.105),(.025,.13,.09,.16),(-.075,.095,.075,.15),(-.17,.045,.045,.10),(-.225,.003,.026,.008)],mats,scale,offset)
    jaw('lower cutting jaw',[(.09,.104,.065,.06),(.015,.11,.06,.055),(-.08,.075,.048,.035),(-.16,.003,.036,.008)],mats,scale,offset,True)
    # Shallow natural growth ridges on the crown, identical on loot and shield.
    ridge=mottled('horn growth ridges',(.045,.024,.029),(.11,.061,.055))
    for y,w,z,h in [(.025,.13,.09,.16),(-.075,.095,.075,.15)]:
        points=[]
        for i in range(11):
            a=.20+(math.pi-.40)*i/10
            points.append(Vector((w*math.cos(a),y,z+h*math.sin(a)+.001))*scale+Vector(offset))
        for a,b in zip(points,points[1:]): rod('subtle horn growth ridge',a,b,.0015*scale,ridge)

def rod(name,a,b,width,mat):
    a,b=Vector(a),Vector(b)
    obj=shapes.cylinder(name,width,(b-a).length,(a+b)/2,material=mat,vertices=6)
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return obj

OUTLINE=[(0,1.03),(.24,.95),(.36,.76),(.33,.46),(.20,.22),(0,.02),(-.20,.22),(-.33,.46),(-.36,.76),(-.24,.95)]

def shield():
    existing=set(bpy.context.scene.objects)
    silver=mottled('hammered silver',(.20,.245,.29),(.66,.72,.76))
    # Individually cut pale finewood planks, with visible joints and longitudinal grain.
    wood=materials.wood('pale finewood',light=(.50,.34,.17),dark=(.24,.135,.056),axis='Z',scale=3.5)
    def clip(poly,bound,greater):
        result=[]
        for a,b in zip(poly,poly[1:]+poly[:1]):
            ain=(a[0]>=bound) if greater else (a[0]<=bound)
            bin=(b[0]>=bound) if greater else (b[0]<=bound)
            if ain: result.append(a)
            if ain != bin:
                t=(bound-a[0])/(b[0]-a[0]); result.append((bound,a[1]+t*(b[1]-a[1])))
        return result
    for j in range(7):
        lo=-.365+j*.1045+.0015; hi=lo+.1015
        outline=clip(clip(OUTLINE,lo,True),hi,False); n=len(outline)
        if n<3: continue
        front=-.04
        verts=[(x,y,z) for y in (front,.085) for x,z in outline]
        faces=[tuple(range(n)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        obj=mesh('finewood plank '+str(j+1),verts,faces,[wood])
        bevel=obj.modifiers.new('worn plank edges','BEVEL'); bevel.width=.003; bevel.segments=1
    # Silver braces secure a single complete beak instead of extra horn plates.
    for side in (-1,1):
        for z in (.33,.82):
            a=Vector((side*.06,-.062,.56)); b=Vector((side*(.225 if z<.5 else .29),-.032,z))
            brace=shapes.box('silver beak brace',(.034,.023,(b-a).length),(a+b)/2,material=silver,bevel=.004)
            brace.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
            shapes.sphere('silver brace rivet',.015,b+Vector((0,-.02,0)),material=silver,segments=8,rings=4)
    for i,(x,z) in enumerate(OUTLINE):
        xx,zz=OUTLINE[(i+1)%10]
        rod('silver perimeter',(x,-.004,z),(xx,-.004,zz),.021,silver)
        shapes.sphere('hammered silver rim rivet',.025,(x*.93,-.028,.54+(z-.54)*.94),material=silver,segments=8,rings=4)
    beak(offset=(0,-.10,.40))
    # Small silver knot ornaments and an inset border leave the finewood visible.
    for z,wide,tall in [(.91,.065,.045),(.19,.042,.037)]:
        loop=[(-wide,z),(0,z+tall),(wide,z),(0,z-tall),(-wide,z)]
        for (x1,z1),(x2,z2) in zip(loop,loop[1:]):
            rod('silver knot inlay',(x1,-.044,z1),(x2,-.044,z2),.005,silver)
        shapes.sphere('knot center pin',.009,(0,-.049,z),material=silver,segments=10,rings=5)
    for i,(x,z) in enumerate(OUTLINE):
        xx,zz=OUTLINE[(i+1)%10]
        a=Vector((x*.88,-.025,.54+(z-.54)*.88))
        b=Vector((xx*.88,-.025,.54+(zz-.54)*.88))
        rod('inset silver border',a,b,.0035,silver)
        for t in (.33,.67):
            shapes.sphere('small rim nail',.006,a.lerp(b,t)+Vector((0,-.005,0)),material=silver,segments=8,rings=4)
    for z in (.37,.76):
        shapes.box('rear silver cross brace',(.55,.022,.036),(0,.095,z),material=silver,bevel=.004)
    # One grip, behind the middle of the board: the fist holds it there, as on the game's own shields (the user).
    rod('silver grip anchor',(0,.08,.43),(0,.16,.45),.018,silver)
    rod('silver grip anchor',(0,.08,.70),(0,.16,.68),.018,silver)
    rod('finewood rear grip',(0,.16,.45),(0,.16,.68),.025,wood)
    for z in (.46,.67):
        shapes.cylinder('silver grip ferrule',.027,.018,(0,.16,z),material=silver,vertices=12)
    # Bow the shield furniture around the arm; keep the central beak identical
    # to the standalone loot model instead of distorting its shared geometry.
    bpy.context.view_layer.update()
    for obj in set(bpy.context.scene.objects)-existing:
        if obj.type!='MESH' or any(tag in obj.name for tag in ('hooked upper beak','cutting jaw','growth ridge')):
            continue
        matrix=obj.matrix_world.copy(); inverse=matrix.inverted()
        # Long cross braces need intermediate vertices to follow the bow.
        if 'rear silver cross brace' in obj.name:
            bm=bmesh.new(); bm.from_mesh(obj.data)
            edges=[e for e in bm.edges if abs((matrix@e.verts[0].co).x-(matrix@e.verts[1].co).x)>.2]
            bmesh.ops.subdivide_edges(bm,edges=edges,cuts=6,use_grid_fill=True)
            bm.to_mesh(obj.data); bm.free()
        for vertex in obj.data.vertices:
            point=matrix@vertex.co; point.y+=.65*point.x*point.x
            vertex.co=inverse@point
    shapes.collider_box('shield',(.765,.70,1.05),(0,-.125,.525))

def build(is_shield=False):
    if is_shield: shield()
    else:
        beak()
        shapes.collider_box('beak',(.45,.55,.42),(0,-.10,.215))
