"""Gravebranch: original low-poly bone crossbow and quarrel. Metres, front -Y."""
import math, random
import bpy
from mathutils import Vector
from workshop import materials, shapes, scene

PI=math.pi

def surface(name, dark, light, scale=90, stretch=(1,1,1)):
    mat=materials.flat(name,dark,roughness=.88)
    t=mat.node_tree; p=materials.principled(mat)
    xyz=t.nodes.new('ShaderNodeTexCoord')
    mul=t.nodes.new('ShaderNodeVectorMath'); mul.operation='MULTIPLY'; mul.inputs[1].default_value=stretch
    t.links.new(xyz.outputs['Object'],mul.inputs[0])
    # Quantized cells keep painted patches broad like the reference game's pixel textures.
    snap=t.nodes.new('ShaderNodeVectorMath'); snap.operation='SNAP'; snap.inputs[1].default_value=(.007,.007,.007)
    t.links.new(mul.outputs[0],snap.inputs[0])
    noise=t.nodes.new('ShaderNodeTexNoise'); noise.inputs['Scale'].default_value=scale; noise.inputs['Detail'].default_value=2
    t.links.new(snap.outputs[0],noise.inputs['Vector'])
    ramp=t.nodes.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation='CONSTANT'
    for i,e in enumerate([ramp.color_ramp.elements[0],ramp.color_ramp.elements[1]]):
        e.position=.24 if i==0 else .73; e.color=(*(dark if i==0 else light),1)
    for f in (.39,.50,.61):
        e=ramp.color_ramp.elements.new(f); a=(f-.24)/.49
        e.color=(*(d*(1-a)+l*a for d,l in zip(dark,light)),1)
    t.links.new(noise.outputs['Fac'],ramp.inputs[0]); t.links.new(ramp.outputs[0],p.inputs['Base Color'])
    return mat

def palette():
    return dict(bone=surface('weathered warm ivory',(.26,.225,.16),(.64,.585,.455),56),
        edge=surface('freshly scraped bone edges',(.47,.425,.32),(.77,.72,.57),65),
        aged=surface('ochre bone roots',(.145,.103,.055),(.40,.315,.185),70),
        wood=surface('dark carved heartwood',(.025,.016,.009),(.115,.063,.029),80,(1,.15,1)),
        leather=surface('smoked russet leather',(.052,.020,.012),(.18,.075,.034),85),
        cord=surface('old flax and sinew',(.18,.12,.062),(.43,.315,.175),130),
        iron=surface('forged black iron',(.024,.027,.028),(.09,.10,.10),70),
        cut=materials.flat('incised dark cuts',(.07,.049,.025),roughness=1),
        feather=surface('raven feather',(.018,.024,.024),(.075,.085,.073),90))

def mesh(name, vertices, faces, mat):
    data=bpy.data.meshes.new(name); data.from_pydata(vertices,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj); data.materials.append(mat)
    scene.select_only([obj]); bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
    return obj

def sweep(name, stations, mat, sides=8):
    """Elliptical sections normal to the path: (x,y,z,width,depth)."""
    vs=[]; fs=[]
    for k,row in enumerate(stations):
        p=Vector(row[:3]); a=Vector(stations[max(0,k-1)][:3]); b=Vector(stations[min(len(stations)-1,k+1)][:3])
        tangent=(b-a).normalized(); up=Vector((0,0,1))
        if abs(tangent.dot(up))>.96: up=Vector((0,1,0))
        across=tangent.cross(up).normalized(); vertical=across.cross(tangent).normalized()
        for i in range(sides):
            angle=2*PI*i/sides
            vs.append(p+across*math.cos(angle)*row[3]+vertical*math.sin(angle)*row[4])
    for k in range(len(stations)-1):
        for i in range(sides):
            a=k*sides+i; b=k*sides+(i+1)%sides; fs.append((a,b,b+sides,a+sides))
    fs.extend([tuple(reversed(range(sides))),tuple((len(stations)-1)*sides+i for i in range(sides))])
    return mesh(name,vs,fs,mat)

def line(name, points, radius, mat, sides=5):
    return sweep(name,[(*p,radius,radius) for p in points],mat,sides)

def wrap_y(name,y0,y1,rx,rz,z,mat,turns=5,thick=.003):
    steps=turns*8
    return line(name,[(rx*math.cos(i/8*2*PI),y0+(y1-y0)*i/steps,z+rz*math.sin(i/8*2*PI)) for i in range(steps+1)],thick,mat,4)

def body(m):
    # The tiller has a slender bolt bed and a downward-swept shoulder stock.
    sweep('carved heartwood tiller',[(0,.34,-.057,.042,.055),(0,.25,-.07,.038,.061),
        (0,.11,-.032,.028,.048),(0,-.05,-.010,.031,.043),(0,-.25,-.005,.034,.045),
        (0,-.47,-.010,.043,.043),(0,-.59,-.005,.025,.030)],m['wood'])
    # Two long natural splints form bone cheeks, preserving the open bolt groove.
    for sign in (-1,1):
        sweep('left bone cheek' if sign<0 else 'right bone cheek',[
            (sign*.026,.34,-.051,.036,.050),(sign*.031,.29,-.055,.032,.047),
            (sign*.028,.18,-.044,.017,.041),(sign*.027,.055,-.005,.010,.042),
            (sign*.034,-.085,.006,.011,.035),(sign*.037,-.27,.008,.014,.037),
            (sign*.046,-.43,.002,.019,.045),(sign*.036,-.54,.005,.018,.034),
            (sign*.021,-.60,.001,.013,.025)],m['bone'])
        # A slim polished rail guides the bolt, while leaving its fletchings clear.
        sweep('polished bolt guide',[(sign*.015,-.08,.040,.005,.005),
            (sign*.015,-.32,.046,.005,.005),(sign*.015,-.58,.029,.004,.004)],m['edge'],6)
    # Knuckled butt has a split in the silhouette rather than a toy ball end.
    sweep('butt heel',[(0,.27,-.073,.042,.034),(0,.34,-.063,.053,.037),
        (0,.38,-.048,.043,.030),(0,.385,-.03,.025,.022)],m['aged'])
    for sign in (-1,1):
        sweep('butt condyle',[(sign*.025,.31,-.05,.023,.045),(sign*.034,.35,-.04,.030,.05),
            (sign*.028,.38,-.026,.021,.033)],m['bone'])
    # Laminated ribs: broad at the socket, tapering into recurved, hooked nocks.
    for sign in (-1,1):
        stations=[(.015,-.475,.001,.048,.039),(.115,-.49,.009,.055,.031),
            (.22,-.455,.022,.051,.026),(.33,-.395,.036,.042,.022),
            (.425,-.335,.050,.033,.018),(.49,-.325,.060,.022,.014),
            (.53,-.365,.070,.012,.010),(.54,-.398,.074,.005,.006)]
        sweep('rib limb L' if sign<0 else 'rib limb R',[(sign*x,y,z,w,h) for x,y,z,w,h in stations],m['bone'])
        # Under-rib visible as a narrower aged lamination, ending short of the hook.
        sweep('aged rib lamination',[(sign*x,y+.006,z-.023,w*.77,h*.40) for x,y,z,w,h in stations[:-2]],m['aged'],6)
        sweep('scraped limb ridge',[(sign*x,y-.018,z+.013,w*.36,h*.52) for x,y,z,w,h in stations[1:-1]],m['edge'],6)
        for j,(x,y,z,w,h) in enumerate([stations[1],stations[3],stations[5]]):
            # Bind across each limb at three structural joins.
            for turn in range(3):
                pts=[]
                for k in range(13):
                    a=2*PI*k/12
                    pts.append((sign*(x+(turn-1)*.010),y+math.cos(a)*(w+.003),z+math.sin(a)*(h+.004)))
                line('limb sinew binding',pts,.0026,m['cord'],4)
        # Small nock notch and its reinforcement, with the actual string endpoint inside it.
        line('nock serving',[(sign*.52,-.348,.060),(sign*.531,-.359,.080),(sign*.517,-.367,.085)],.005,m['cord'])
    # Socket lashings and a low rib arch under the body echo the bone tower shield.
    wrap_y('socket broad leather',-.52,-.46,.063,.054,0,m['leather'],4,.007)
    wrap_y('socket sinew tie',-.516,-.477,.067,.058,0,m['cord'],4,.003)
    for sign in (-1,1):
        sweep('wishbone foregrip',[(sign*.052,-.47,-.013,.014,.019),
            (sign*.071,-.35,-.053,.014,.019),(sign*.052,-.24,-.088,.012,.015),
            (sign*.027,-.18,-.038,.009,.010)],m['bone'],6)
    grip=[]
    for i in range(9*8+1):
        t=i/(9*8); a=i/8*2*PI
        grip.append(((.051-.005*t)*math.cos(a),.025+.145*t,-.010-.029*t+.053*math.sin(a)))
    line('hand wrapped leather',grip,.006,m['leather'],4)
    wrap_y('grip end serving',.018,.038,.054,.056,-.012,m['cord'],3,.002)
    wrap_y('grip heel serving',.159,.177,.049,.056,-.038,m['cord'],3,.002)
    # Trigger and rolling nut are functional-looking, visibly separate from the grip.
    shapes.cylinder('rolling antler nut',.019,.071,(0,-.055,.025),(0,PI/2,0),m['aged'],vertices=10)
    shapes.cylinder('iron axle pin',.007,.078,(0,-.055,.025),(0,PI/2,0),m['iron'],vertices=8)
    line('forged trigger lever',[(0,-.065,-.023),(0,-.055,-.087),(0,.003,-.098),(0,.027,-.074)],.005,m['iron'],6)
    # A bone foot stirrup is part of the silhouette, with a genuinely open centre.
    line('bone cocking stirrup',[(-.030,-.574,-.008),(-.077,-.632,-.015),(-.066,-.695,-.020),
        (0,-.723,-.022),(.066,-.695,-.020),(.077,-.632,-.015),(.030,-.574,-.008)],.011,m['aged'],6)
    line('stirrup worn toe',[(-.066,-.695,-.014),(0,-.723,-.016),(.066,-.695,-.014)],.009,m['edge'],6)
    # Restrained carved tally marks on the bone cheek, no glowing embellishments.
    for sign in (-1,1):
        for j in range(4):
            y=-.17-j*.043
            line('cut diagonal',[(sign*.047,y-.012,.020),(sign*.050,y,.008),(sign*.048,y+.012,.018)],.0017,m['cut'],3)
    for y in (-.20,-.39):
        wrap_y('forestock binding',y-.011,y+.011,.054,.047,0,m['cord'],3,.0025)

def string(m,loaded=True):
    mid=-.055 if loaded else -.34
    return line('drawn sinew string' if loaded else 'released sinew string',
        [(-.521,-.36,.062),(0,mid,.057),(.521,-.36,.062)],.0027,m['cord'],6)

def bolt(m):
    # A carved splinter quarrel, bone from nock to point, with three raven flights.
    sweep('bone quarrel shaft',[(0,.02,0,.007,.008),(0,-.025,0,.010,.010),
        (0,-.22,0,.007,.007),(0,-.35,0,.009,.008),(0,-.40,0,.006,.006)],m['bone'],6)
    sweep('knapped bone broadhead',[(0,-.33,0,.009,.008),(0,-.37,0,.023,.010),
        (0,-.415,.001,.017,.008),(0,-.49,0,.0006,.0006)],m['edge'],4)
    # Sharp side shoulders are asymmetric chips, giving an intentionally hand-worked point.
    for sign in (-1,1):
        mesh('broadhead barb',[(sign*.009,-.365,0),(sign*.028,-.344,0),(sign*.021,-.389,0),
            (sign*.014,-.368,.005),(sign*.014,-.368,-.005)],[(0,1,3),(1,2,3),(2,0,3),(1,0,4),(2,1,4),(0,2,4)],m['bone'])
    wrap_y('head sinew',-.345,-.318,.010,.010,0,m['cord'],5,.0014)
    for k in range(3):
        a=2*PI*k/3+PI/2
        def p(r,y,offset=0): return (math.cos(a)*r+math.sin(a)*offset,y,math.sin(a)*r-math.cos(a)*offset)
        outline=[(.008,-.018),(.032,-.002),(.036,-.025),(.032,-.049),(.030,-.054),
            (.031,-.060),(.022,-.090),(.008,-.12)]
        vs=[p(r,y,off) for off in (-.0007,.0007) for r,y in outline]; n=len(outline)
        fs=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        mesh('raven flight',vs,fs,m['feather'])
        line('flight quill',[p(.011,-.115),p(.020,-.065),p(.029,-.01)],.0012,m['aged'],4)
    wrap_y('flight serving',-.115,-.099,.009,.009,0,m['cord'],4,.001)
    wrap_y('nock serving',.002,.014,.009,.009,0,m['cord'],3,.001)

def build_crossbow(loaded=True):
    m=palette(); body(m); string(m,loaded)
    shapes.collider_box('tiller',(.115,1.00,.17),(0,-.16,-.025))
    shapes.collider_box('limbs',(1.08,.18,.10),(0,-.425,.026))

def build_bolt():
    bolt(palette()); shapes.collider_box('bolt',(.035,.51,.035),(0,-.235,0))
