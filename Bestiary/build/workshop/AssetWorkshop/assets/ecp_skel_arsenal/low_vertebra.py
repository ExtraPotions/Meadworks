"""Purpose-built low-poly vertebrae. Large guards keep a canal; shafts use solid crests."""
import math
import bmesh
from mathutils import Vector
import low_shapes as gs
from grave_vertebra import frame, at, canal, CANAL, SPINE_ROOT, WING_ROOT, KINDS


def build(prefix, place, r, bone, kind='lumbar', spine=True, wings=True,
          pegs=True, low=False, height=None):
    k = KINDS[kind]
    h = height if height is not None else k['h']
    mesh = bmesh.new()
    rings = []
    for z, scale in ((-.5,.92),(-.30,1.08),(.12,.76),(.5,1.0)):
        rings.append([mesh.verts.new((r*scale*math.cos(a),
                                     .78*r*scale*math.sin(a), z*h*r))
                      for a in (math.tau*(i+.5)/6 for i in range(6))])
    gs.skin(mesh, rings, 6)
    parts = [gs.mesh_object(prefix+'_body', mesh, bone, False)]
    if not low:
        # One open pentagonal arch, a readable hole only on the focal head/guard.
        path = [(-.55,.15),(-.65,1.05),(0,1.65),(.65,1.05),(.55,.15)]
        parts.append(gs.band(prefix+'_canal', [(Vector((x*r,y*r,0)),.33*r,.22*r)
                                             for x,y in path], bone, sides=4))
    if spine:
        reach = k['spine'][-1][0]
        end_z = k['spine'][-1][1]
        root = 1.45 if not low else .45
        parts.append(gs.band(prefix+'_crest',
                             [(Vector((0,root*r,.12*r)), .30*r, .24*r),
                              (Vector((0,(root+reach)*.5*r,end_z*.4*r)), .29*r,.17*r),
                              (Vector((0,reach*r,end_z*r)), .12*r,.055*r)], bone, sides=4))
    if wings:
        reach, rise = k['wing']
        for side in (-1,1):
            parts.append(gs.cone(prefix+'_wing_'+str(side),
                                 (side*.45*r,.3*r,.08*r),
                                 (side*reach*r,(.9 if not low else .45)*r,rise*r),
                                 .30*r,bone,tip_ratio=.36))
    gs.transform(parts, place)
    return parts


def flat(prefix, front, r, bone, kind='lumbar', wings=True, pegs=True):
    centre = front + .8*r
    place = frame(gs.at(centre), (0,0,1), (0,-1,0))
    build(prefix, place, r, bone, kind, spine=False, wings=wings)
    return place, centre + SPINE_ROOT[1]*r


def column(name, s0, s1, r0, r1, bone, disc=None, kind='shaft', back=(1,0,0),
           turn=0, length=1, count=None, **parts):
    count = count or max(2, round((s1-s0)/.09))
    step = (s1-s0)/count
    made = []
    for i in range(count):
        r = r0 + (r1-r0)*(i+.5)/count
        angle = math.radians(turn*i)
        direction = (back[0]*math.cos(angle)-back[2]*math.sin(angle),0,
                     back[0]*math.sin(angle)+back[2]*math.cos(angle))
        centre = s0 + (i+.5)*step
        made += build(f'{name}_{i}', frame(gs.at(centre),(0,-1,0),direction),
                      r,bone,kind,low=True,height=step*.96/r,**parts)
    if disc:
        made.append(gs.loft(name+'_recessed_core', [(s0,.52*r0,.48*r0),
                                                   (s1,.52*r1,.48*r1)], disc))
    return made
