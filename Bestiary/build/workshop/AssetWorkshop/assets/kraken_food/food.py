"""Original raw/cooked kraken portions. Palette follows our kraken; vanilla serpent meat informs the low-poly finish."""
import math
import bpy
from mathutils import Vector
from workshop import shapes,materials


def painted(name,dark,light,scale=28):
    mat=materials.flat(name,dark,roughness=.8)
    tree=mat.node_tree
    noise=tree.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=scale; noise.inputs['Detail'].default_value=2
    coord=tree.nodes.new('ShaderNodeTexCoord'); tree.links.new(coord.outputs['Object'],noise.inputs['Vector'])
    ramp=tree.nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=(*dark,1); ramp.color_ramp.elements[0].position=.25
    ramp.color_ramp.elements[1].color=(*light,1); ramp.color_ramp.elements[1].position=.75
    tree.links.new(noise.outputs['Fac'],ramp.inputs['Fac'])
    tree.links.new(ramp.outputs['Color'],materials.principled(mat).inputs['Base Color'])
    return mat


def mesh(name,verts,faces,mats,indices=None):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
    for mat in mats: data.materials.append(mat)
    if indices:
        for face,index in zip(data.polygons,indices): face.material_index=index
    return obj


def cup(center,normal,radius,rim,pit,name):
    normal=normal.normalized(); u=normal.cross(Vector((1,0,0))).normalized(); v=normal.cross(u)
    verts=[]
    for r,h in [(radius*.86,0),(radius,.004),(radius*.57,.007),(radius*.43,.0015)]:
        for i in range(10):
            a=2*math.pi*i/10
            verts.append(center+normal*h+(u*math.cos(a)+v*math.sin(a))*r)
    faces=[(j*10+i,j*10+(i+1)%10,(j+1)*10+(i+1)%10,(j+1)*10+i) for j in range(3) for i in range(10)]
    faces.append(tuple(range(30,40)))
    mesh(name,verts,faces,[rim,pit],[0]*30+[1])


def build(cooked=False):
    skin=painted('roasted_skin' if cooked else 'kraken_skin',
                 (.045,.014,.009) if cooked else (.025,.006,.018),
                 (.24,.092,.030) if cooked else (.135,.032,.043))
    flesh=painted('cooked_flesh' if cooked else 'raw_flesh',
                  (.30,.19,.075) if cooked else (.40,.245,.225),
                  (.62,.44,.205) if cooked else (.72,.56,.45),38)
    rim=painted('toasted_suckers' if cooked else 'sucker_rims',
                (.18,.085,.027) if cooked else (.39,.225,.19),
                (.52,.31,.102) if cooked else (.68,.47,.36))
    pit=materials.flat('sucker_centers',(.035,.017,.006) if cooked else (.105,.023,.032))
    points=[(-.105,-.025,.059),(-.066,-.023,.060),(-.022,-.012,.053),(.022,.010,.044),
            (.048,.043,.034),(.034,.071,.026),(.002,.077,.019),(-.022,.056,.013)]
    radii=[.052,.052,.046,.036,.028,.021,.015,.009]
    if cooked:
        points=[(x*.91,y*.92,z*.89) for x,y,z in points]
        radii=[r*.89 for r in radii]
    points=[Vector(p) for p in points]; count=12; verts=[]; frames=[]
    for j,(center,r) in enumerate(zip(points,radii)):
        tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized()
        u=tangent.cross(Vector((0,0,1))).normalized(); v=tangent.cross(u).normalized()
        frames.append((u,v,tangent))
        for i in range(count):
            a=2*math.pi*i/count
            verts.append(center+r*(u*math.cos(a)+v*math.sin(a)))
    faces=[]; indices=[]
    for j in range(len(points)-1):
        for i in range(count):
            faces.append((j*count+i,j*count+(i+1)%count,(j+1)*count+(i+1)%count,(j+1)*count+i))
            # Continuous mottled skin avoids solid patches bounded by whole faces.
            indices.append(0)
    faces.append(tuple(reversed(range(count)))); indices.append(1)
    faces.append(tuple((len(points)-1)*count+i for i in range(count))); indices.append(0)
    mesh('curled_tentacle_portion',verts,faces,[skin,flesh,rim],indices)
    # Concentric flesh rings on the exposed cut, kept subtle rather than a bone in the middle.
    u,v,tangent=frames[0]; center=points[0]-tangent*.0007
    ring=[]
    for r in (radii[0]*.75,radii[0]*.72):
        ring.extend([center+r*(u*math.cos(i*2*math.pi/count)+v*math.sin(i*2*math.pi/count)) for i in range(count)])
    mesh('cut_flesh_grain',ring,[(i,(i+1)%count,count+(i+1)%count,count+i) for i in range(count)],[rim])
    for j in range(1,7):
        u,v,tangent=frames[j]
        for row in (0,1):
            a=.24+row*.8
            n=(u*math.cos(a)-v*math.sin(a)).normalized()
            center=points[j]+n*radii[j]*.985+tangent*(.004 if row else -.007)
            cup(center,n,radii[j]*.24,rim,pit,f'sucker_{j}_{row}')
    # Body collider stays inside the edible portion; it needs no rigidbody or gameplay scripts in the art bundle.
    shapes.collider_box('portion',(.21 if not cooked else .19,.16 if not cooked else .145,.108 if not cooked else .098),
                        (-.025,.005,.056 if not cooked else .050))
