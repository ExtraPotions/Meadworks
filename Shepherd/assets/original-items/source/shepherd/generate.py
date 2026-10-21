"""Build the three original Shepherd feeding pieces. Copyright 2026 expDARE.

SPDX-License-Identifier: GPL-3.0-or-later
"""
import json
import math
from pathlib import Path
from geometry import box, lathe, mesh, transform, validate

WOOD=(0.36,0.205,0.095,1)
WOOD_LIGHT=(0.47,0.285,0.14,1)
WOOD_DARK=(0.235,0.125,0.06,1)
IRON=(0.17,0.18,0.17,1)
STONE=(0.33,0.34,0.31,1)
RESIN=(0.14,0.078,0.036,1)


def asset(name, title, parts, surface, area, collider):
    return {"id":name,"displayName":title,"prefabs":[name],"kind":"build-piece",
            "parts":parts,"icon":{"position":[2.8,2.0,3.2],"target":[0,0.3,0],"orthographicSize":1.3},
            "anchors":[{"name":"food_surface","position":surface},
                       {"name":"food_surface_min","position":[surface[0]-area[0]/2,surface[1],surface[2]-area[2]/2]},
                       {"name":"food_surface_max","position":[surface[0]+area[0]/2,surface[1],surface[2]+area[2]/2]}],
            "collider":collider,"attachment":{"foodSurface":{"center":surface,"size":area},
            "foodDisplay":"actual stored edible native item meshes only; clear when empty; no baked food or straw",
            "collision":"individual shell/floor mesh parts only; do not apply the aggregate collider bounds"}}


def stave(i, segments, top):
    a=i*math.tau/segments+0.012; b=(i+1)*math.tau/segments-0.012
    vertices=[]
    for r,y in [(0.43,0.08),(0.50,top),(0.425,top),(0.355,0.08)]:
        for angle in (a,b): vertices.append([r*math.cos(angle),y,r*math.sin(angle)])
    return mesh(f"oak_stave_{i:02}",WOOD_LIGHT if i%3==0 else WOOD,vertices,
                [(0,2,3,1),(2,4,5,3),(4,6,7,5),(6,0,1,7),(0,6,4,2),(1,3,5,7)],collision=True)


def hoop(name,y,first=0,last=16):
    verts=[]; faces=[]
    for i in range(first,last+1):
        angle=i*math.tau/16
        for r,h in ((0.507,y-0.027),(0.507,y+0.027),(0.491,y+0.027),(0.491,y-0.027)):
            verts.append([r*math.cos(angle),h,r*math.sin(angle)])
    for i in range(last-first):
        for j in range(4): faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
    faces.extend([(3,2,1,0),tuple((last-first)*4+j for j in range(4))])
    return mesh(name,IRON,verts,faces,"iron")


def barrel():
    parts=[]
    for i in range(16):
        front=math.sin((i+0.5)*math.tau/16)
        parts.append(stave(i,16,0.44 if front>0.35 else 0.68 if front<-0.35 else 0.56))
    parts.append(lathe("oak_floor",WOOD_DARK,[(0.355,0.09),(0.385,0.145)],16,collision=True))
    parts.extend([hoop("lower_iron_hoop",0.20),hoop("rear_iron_hoop",0.56,8,16)])
    # Two separate lid planks lean behind the opening; the basin remains uncovered.
    for x,w in [(-0.17,0.32),(0.17,0.32)]:
        parts.append(box("leaning_lid_plank",WOOD_LIGHT,(x,0.42,-0.525),(w,0.62,0.052),rotation=(-18,0,0),collision=True))
    for y in (0.22,0.58):
        parts.append(box("lid_cross_batten",WOOD_DARK,(0,y,-0.57),(0.66,0.06,0.05),rotation=(-18,0,0),collision=True))
    for i in range(0,16,2):
        angle=i*math.tau/16
        parts.append(lathe(f"hoop_rivet_{i}",IRON,[(0.017,-0.009),(0.022,0.005),(0.015,0.012)],6,"iron",
                           center=(0.51*math.cos(angle),0.20,0.51*math.sin(angle)),rotation=(0,0,90)))
    result=asset("shepherd_feed_barrel","Feed Barrel",parts,[0,0.15,0.025],[0.61,0.27,0.54],
                 {"center":[0,0.4,-0.045],"size":[1.06,0.80,1.18]})
    result["attachment"]["frontRimHeight"]=0.44
    result["attachment"]["innerFloorHeight"]=0.145
    result["icon"]["orthographicSize"]=0.82
    return result


def plank():
    parts=[]
    for z in (-0.225,0,0.225):
        parts.append(box("joined_floor_plank",WOOD_DARK,(0,0.16,z),(1.84,0.085,0.217),collision=True))
    for z in (-0.373,0.373):
        for y in (0.265,0.395):
            parts.append(box("long_rough_side_plank",WOOD if y<0.3 else WOOD_LIGHT,(0,y,z),(2.02,0.13,0.09),collision=True))
        parts.append(box("sealed_floor_seam",RESIN,(0,0.21,z*0.85),(1.79,0.013,0.015)))
    for x in (-0.965,0.965):
        for y in (0.265,0.395):
            parts.append(box("end_plank",WOOD,(x,y,0),(0.09,0.13,0.66),collision=True))
        for z in (-0.38,0.38):
            parts.append(box("corner_post",WOOD_LIGHT,(x,0.32,z),(0.13,0.34,0.12),collision=True))
            parts.append(box("splayed_stub_foot",WOOD_DARK,(x,0.105,z),(0.16,0.24,0.16),rotation=(0,0,13 if x>0 else -13),collision=True))
            parts.append(box("forged_square_nail",IRON,(x,0.385,z+(0.065 if z>0 else -0.065)),(0.037,0.037,0.014),"iron"))
    result=asset("shepherd_trough_plank","Plank Trough",parts,[0,0.205,0],[1.70,0.20,0.52],
                 {"center":[0,0.245,0],"size":[2.18,0.49,0.94]})
    result["attachment"]["frontRimHeight"]=0.46
    result["attachment"]["innerFloorHeight"]=0.2025
    return result


def rough_block(name,center,size,index):
    halves=[v/2 for v in size]; bevel=min(size)*.18
    signs=[(x,y,z) for x in (-1,1) for y in (-1,1) for z in (-1,1)]
    vertices=[]; lookup={}
    for corner,sign in enumerate(signs):
        for axis in range(3):
            lookup[(sign,axis)]=len(vertices)
            vertices.append([sign[j]*(halves[j]-(0 if j==axis else bevel))+
                             (0.011 if j!=1 else .014)*math.sin(index*7+corner*3+j) for j in range(3)])
    faces=[]
    def outward(indices):
        points=[vertices[i] for i in indices]
        middle=[sum(v[j] for v in points)/len(points) for j in range(3)]
        normal=middle
        length=math.sqrt(sum(v*v for v in normal)); normal=[v/length for v in normal]
        axis=[0,1,0] if abs(normal[1])<.9 else [1,0,0]
        u=[normal[1]*axis[2]-normal[2]*axis[1],normal[2]*axis[0]-normal[0]*axis[2],normal[0]*axis[1]-normal[1]*axis[0]]
        v=[normal[1]*u[2]-normal[2]*u[1],normal[2]*u[0]-normal[0]*u[2],normal[0]*u[1]-normal[1]*u[0]]
        return sorted(indices,key=lambda i:math.atan2(sum((vertices[i][j]-middle[j])*v[j] for j in range(3)),sum((vertices[i][j]-middle[j])*u[j] for j in range(3))))
    for axis in range(3):
        for side in (-1,1): faces.append(outward([lookup[(s,axis)] for s in signs if s[axis]==side]))
    for a,b in ((0,1),(0,2),(1,2)):
        for sa in (-1,1):
            for sb in (-1,1):
                faces.append(outward([lookup[(s,axis)] for s in signs if s[a]==sa and s[b]==sb for axis in (a,b)]))
    for s in signs: faces.append(outward([lookup[(s,axis)] for axis in range(3)]))
    color=tuple(c*(0.88+(index%5)*0.055) if j<3 else c for j,c in enumerate(STONE))
    return transform(mesh(name,color,vertices,faces,"stone",collision=True),center)


def stone():
    parts=[]
    for x in (-0.73,0.73):
        parts.append(box("low_wood_sleeper",WOOD_DARK,(x,0.075,0),(0.17,0.13,0.90),collision=True))
    parts.append(rough_block("stone_basin_floor",(0,0.155,0),(2.06,0.11,0.82),0))
    for zi,z in enumerate((-0.397,0.397)):
        for i in range(5):
            parts.append(rough_block("dressed_stone_long_wall",(-0.83+i*0.415,0.345,z),(0.43,0.31,0.18),i+zi*7+1))
    for xi,x in enumerate((-1.04,1.04)):
        for i,z in enumerate((-0.19,0.19)):
            parts.append(rough_block("dressed_stone_end",(x,0.345,z),(0.17,0.31,0.39),18+xi*3+i))
    result=asset("shepherd_trough_stone","Stone Trough",parts,[0,0.217,0],[1.74,0.20,0.53],
                 {"center":[0,0.26,0],"size":[2.28,0.52,0.98]})
    result["attachment"]["frontRimHeight"]=0.514
    result["attachment"]["innerFloorHeight"]=0.217
    return result


def export(product,assets,path):
    materials={}
    for a in assets:
        validate(a["parts"])
        for part in a["parts"]:
            color=part.pop("color")
            mid=part["material"]+"_"+"".join(f"{round(c*255):02x}" for c in color[:3])
            metal=part["material"]=="iron"
            materials[mid]={"id":mid,"color":color,"roughness":0.92 if not metal else 0.68,
                            "metallic":0.5 if metal else 0,"emission":
                            [0.13,0.035,0.23] if part["material"]=="eitr" else
                            [0.14,0.025,0.003] if part["material"]=="ember" else [0,0,0]}
            part["material"]=mid
            part["vertices"]=[c for v in part["vertices"] for c in v]
            part["collidable"]=part.pop("collision")
            part["state"]="always"
    doc={"schema":1,"product":product,"units":"meters","materials":list(materials.values()),"assets":assets}
    Path(path).write_text(json.dumps(doc,separators=(",",":"))+"\n",encoding="utf-8")
    return doc


if __name__=="__main__":
    assets=[barrel(),plank(),stone()]
    triangles=sum(validate(a["parts"]) for a in assets)
    export("Shepherd",assets,Path(__file__).with_name("models.json"))
    print(f"Shepherd: {len(assets)} models; {triangles} nondegenerate triangles; hollow walls and floor validated")
