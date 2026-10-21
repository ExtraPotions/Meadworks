# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Original one-chest mesh recipe. Unity axes: meters, Y up, +Z front. No native asset inputs."""
import argparse
import hashlib
import json
import math
import shutil
from pathlib import Path

ASSET = 'mw_raid_chest_model'
HINGE = [0, .86, -.59]
PALETTE = ['#65452e', '#292b2e', '#b78a43', '#25211e']


def sub(a,b): return [a[i]-b[i] for i in range(3)]
def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def unit(v):
    length=math.sqrt(sum(x*x for x in v))
    return [x/length for x in v]


class Mesh:
    def __init__(self,name):
        self.data={'name':name,'vertices':[],'normals':[],'uv':[],'triangles':[]}

    def face(self,points,color):
        normal=unit(cross(sub(points[1],points[0]),sub(points[2],points[0])))
        base=len(self.data['vertices'])//3
        # Four interior atlas tiles: face UVs do not touch neighboring colors.
        uv=[(.02,.02),(.98,.02),(.98,.98),(.02,.98)]
        for i,point in enumerate(points):
            self.data['vertices'].extend(point)
            self.data['normals'].extend(normal)
            self.data['uv'].extend([(color+uv[i][0])/4,uv[i][1]])
        for i in range(1,len(points)-1): self.data['triangles'].extend([base,base+i,base+i+1])

    def box(self,center,size,color):
        x,y,z=[size[i]/2 for i in range(3)]
        corners=[(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
        points=[[p[i]+center[i] for i in range(3)] for p in corners]
        for ids in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)]:
            self.face([points[i] for i in ids],color)

    def prism(self,outline,z,depth,color):
        # Clockwise outline viewed from the front, checked by the signed polygon area.
        if sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))<0:
            outline=list(reversed(outline))
        front=[[x,y,z+depth/2] for x,y in outline]
        back=[[x,y,z-depth/2] for x,y in outline]
        # The clasp is convex so a triangle fan stays inside the outline.
        for i in range(1,len(outline)-1):
            self.face([front[0],front[i],front[i+1]],color)
            self.face([back[0],back[i+1],back[i]],color)
        for i in range(len(outline)):
            j=(i+1)%len(outline)
            self.face([back[i],back[j],front[j],front[i]],color)


def points(mesh): return list(zip(*[iter(mesh.data['vertices'])]*3))
def bounds(values): return ([min(p[i] for p in values) for i in range(3)],[max(p[i] for p in values) for i in range(3)])
def box_for(name,values):
    low,high=bounds(values)
    return {'name':name,'center':[(low[i]+high[i])/2 for i in range(3)],'size':[high[i]-low[i] for i in range(3)]}


def horn():
    mesh=Mesh('horn_mesh')
    centers=[];radii=[];outer=[];inner=[]
    a,b,c=(-.58,.23,.40),(-.36,.83,.42),(.58,.53,.60)
    for k in range(13):
        t=k/12
        center=[(1-t)**2*a[i]+2*(1-t)*t*b[i]+t*t*c[i] for i in range(3)]
        tangent=unit([2*(1-t)*(b[i]-a[i])+2*t*(c[i]-b[i]) for i in range(3)])
        side=unit(cross(tangent,[0,0,1]));up=cross(tangent,side)
        radius=.023+.125*t*t
        centers.append(center);radii.append(radius)
        rings=[]
        for scale in (1,.76):
            rings.append([[center[i]+scale*radius*(math.cos(j*math.tau/8)*side[i]+math.sin(j*math.tau/8)*up[i]) for i in range(3)] for j in range(8)])
        outer.append(rings[0]);inner.append(rings[1])
    for k in range(12):
        for j in range(8):
            n=(j+1)%8
            face=[outer[k][j],outer[k+1][j],outer[k+1][n],outer[k][n]]
            mid=[sum(p[i] for p in face)/4-(centers[k][i]+centers[k+1][i])/2 for i in range(3)]
            if sum(x*y for x,y in zip(cross(sub(face[1],face[0]),sub(face[2],face[0])),mid))<0: face.reverse()
            mesh.face(face,2 if k in (1,10,11) else 3)
            face=[inner[k][j],inner[k][n],inner[k+1][n],inner[k+1][j]]
            mid=[sum(p[i] for p in face)/4-(centers[k][i]+centers[k+1][i])/2 for i in range(3)]
            if sum(x*y for x,y in zip(cross(sub(face[1],face[0]),sub(face[2],face[0])),mid))>0: face.reverse()
            mesh.face(face,3)
    for k in (0,12):
        for j in range(8):
            n=(j+1)%8
            mesh.face([outer[k][j],outer[k][n],inner[k][n],inner[k][j]] if k==12 else [outer[k][n],outer[k][j],inner[k][j],inner[k][n]],2)
    return mesh,centers[-1]


def build():
    body,lid=Mesh('body_mesh'),Mesh('lid_mesh')
    body.box([0,.10,0],[1.5,.15,1.10],0)
    for row in range(4):
        y=.25+row*.16
        for z in (-.52,.52): body.box([0,y,z],[1.46,.145,.055],0)
        for x in (-.72,.72): body.box([x,y,0],[.055,.145,1.04],0)
    for z in (-.5325,.5325): body.box([0,.80,z],[1.52,.065,.055],1)
    for x in (-.7325,.7325): body.box([x,.80,0],[.055,.065,1.01],1)
    # Original square bands and a faceted gold clasp, not a native chest mesh.
    for x in (-.64,.64):
        for z in (-.555,.555): body.box([x,.46,z],[.065,.69,.035],1)
    body.prism([(-.12,.66),(0,.77),(.12,.66),(.09,.48),(0,.42),(-.09,.48)],.585,.05,2)
    body.box([0,.59,.62],[.025,.13,.025],3)
    for i in range(8): lid.box([-.70+i*.20,.09,.56],[.19,.18,1.12],0)
    for x in (-.64,.64): lid.box([x,.19,.56],[.075,.035,1.10],1)
    lid.box([0,.095,.023],[1.58,.18,.07],1)
    lid.box([0,.095,1.097],[1.58,.18,.07],1)
    horn_mesh,mouth=horn()
    direction=unit([.94,-.30,.18])
    mouth_rotation=[-math.degrees(math.asin(direction[1])),math.degrees(math.atan2(direction[0],direction[2])),0]
    nodes=[
        {'name':'model','parent':'','position':[0,0,0],'rotation':[0,0,0],'mesh':''},
        {'name':'body','parent':'model','position':[0,0,0],'rotation':[0,0,0],'mesh':body.data['name']},
        {'name':'lid','parent':'model','position':HINGE,'rotation':[0,0,0],'mesh':lid.data['name']},
        {'name':'horn','parent':'lid','position':[0,0,0],'rotation':[0,0,0],'mesh':horn_mesh.data['name']},
        {'name':'horn_mouth','parent':'horn','position':mouth,'rotation':mouth_rotation,'mesh':''}]
    body_points=points(body)
    lid_points=[tuple(p[i]+HINGE[i] for i in range(3)) for p in points(lid)]
    horn_points=[tuple(p[i]+HINGE[i] for i in range(3)) for p in points(horn_mesh)]
    low,high=bounds(body_points+lid_points+horn_points)
    return {'asset':ASSET,'schema':1,'palette':PALETTE,'textureSize':256,'icon':'raid-chest.svg','hornMouth':mouth,'hornDirection':direction,
            'nodes':nodes,'meshes':[body.data,lid.data,horn_mesh.data],
            'boxes':[box_for('body',body_points),box_for('closed_lid',lid_points),box_for('horn',horn_points)],'boundsMin':low,'boundsMax':high}


def encoded(data): return (json.dumps(data,sort_keys=True,separators=(',',':'),allow_nan=False)+'\n').encode('utf-8')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--out',type=Path,default=Path(__file__).parent/'build/source');parser.add_argument('--stage',action='store_true');args=parser.parse_args()
    root=Path(__file__).resolve().parent
    if not args.out.resolve().is_relative_to(root/'build'): parser.error('--out must stay inside this source pack build directory')
    data=build()
    from validate_source import check
    triangles=check(data)
    args.out.mkdir(parents=True,exist_ok=True)
    output=args.out/'raid-chest.meshes.json';output.write_bytes(encoded(data))
    print(f'{output.resolve()}: {triangles} triangles; SHA256 {hashlib.sha256(output.read_bytes()).hexdigest()}')

    if args.stage:
        project=root/'build/unity'
        for relative in ('Assets/Editor/BuildRaidChest.cs','Packages/manifest.json'):
            dest=project/relative;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/'unity'/relative,dest)
        (project/'ProjectSettings').mkdir(parents=True,exist_ok=True)
        (project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.0.75f1\nm_EditorVersionWithRevision: 6000.0.75f1 (26349cd2a5c8)\n',encoding='utf-8')
        print('Staged source-only Unity project: '+str(project))
