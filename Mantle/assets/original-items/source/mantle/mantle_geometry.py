# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Editable original Mantle geometry, in meters with Y up and display face +Z."""
import math


def sub(a, b): return [a[i] - b[i] for i in range(3)]
def cross(a, b): return [a[1]*b[2]-a[2]*b[1], a[2]*b[0]-a[0]*b[2], a[0]*b[1]-a[1]*b[0]]
def unit(a):
    length = math.sqrt(sum(v*v for v in a))
    if length < 1e-12: raise ValueError('Zero direction')
    return [v/length for v in a]


class Geometry:
    def __init__(self):
        self.parts = []

    def part(self, name, material):
        item = {'name': name, 'material': material, 'vertices': [], 'normals': [], 'uv': [], 'triangles': []}
        self.parts.append(item)
        return item

    def face(self, part, points):
        uv = [(0, 0), (1, 0), (1, 1), (0, 1)]
        # Curved straps have nonplanar quads; split normals at each triangle for native faceting.
        for i in range(1, len(points)-1):
            ids=(0,i,i+1)
            normal=unit(cross(sub(points[i],points[0]),sub(points[i+1],points[0])))
            at=len(part['vertices'])//3
            for j in ids:
                part['vertices'].extend(points[j]);part['normals'].extend(normal);part['uv'].extend(uv[j%4])
            part['triangles'].extend((at,at+1,at+2))

    def box(self, name, mat, center, size):
        p = self.part(name, mat)
        x, y, z = [v/2 for v in size]
        corners = [(-x,-y,-z),(x,-y,-z),(x,y,-z),(-x,y,-z),(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
        points = [[v[i]+center[i] for i in range(3)] for v in corners]
        for ids in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(3,7,6,2),(0,4,7,3),(1,2,6,5)):
            self.face(p, [points[i] for i in ids])
        return p

    def profile(self, name, mat, outline, z, depth):
        p = self.part(name, mat)
        if sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline))) < 0:
            outline = list(reversed(outline))
        front = [[x,y,z+depth/2] for x,y in outline]
        back = [[x,y,z-depth/2] for x,y in outline]
        for i in range(1,len(outline)-1):
            self.face(p, [front[0],front[i],front[i+1]])
            self.face(p, [back[0],back[i+1],back[i]])
        for i in range(len(outline)):
            j = (i+1)%len(outline)
            self.face(p,[back[i],back[j],front[j],front[i]])
        return p

    def tube(self, name, mat, points, radii, sides=6):
        p = self.part(name, mat)
        rings = []
        for i, center in enumerate(points):
            tangent = unit(sub(points[min(i+1,len(points)-1)], points[max(i-1,0)]))
            side = unit(cross(tangent, [0,0,1] if abs(tangent[2]) < .9 else [0,1,0]))
            up = cross(tangent, side)
            r = radii if isinstance(radii,(float,int)) else radii[i]
            rings.append([[center[k]+r*(math.cos(j*math.tau/sides)*side[k]+math.sin(j*math.tau/sides)*up[k]) for k in range(3)] for j in range(sides)])
        self.face(p, list(reversed(rings[0]))) if sides <= 4 else self.cap(p, rings[0], True)
        self.cap(p, rings[-1], False)
        for i in range(len(rings)-1):
            for j in range(sides):
                n = (j+1)%sides
                self.face(p,[rings[i][j],rings[i][n],rings[i+1][n],rings[i+1][j]])
        return p

    def cap(self, part, ring, reverse):
        center = [sum(p[k] for p in ring)/len(ring) for k in range(3)]
        for i in range(len(ring)):
            points = [center,ring[i],ring[(i+1)%len(ring)]]
            self.face(part, list(reversed(points)) if reverse else points)

    def sack(self, name, mat, width, height, depth, y=-.12, z=.23):
        p = self.part(name, mat)
        rings = []
        for level, size in ((-.5,.65),(-.36,.95),(.12,1),(.42,.84),(.5,.61)):
            ring = []
            for j in range(10):
                a = j*math.tau/10
                ring.append([math.cos(a)*width*.5*size,y+height*level,z+math.sin(a)*depth*.5*size])
            rings.append(ring)
        self.cap(p, rings[0], False)
        self.cap(p, rings[-1], True)
        for i in range(len(rings)-1):
            for j in range(10):
                n = (j+1)%10
                self.face(p,[rings[i][j],rings[i+1][j],rings[i+1][n],rings[i][n]])
        return p

    def annulus(self, name, mat, outer, inner, depth, segments=16, center=(0,0,0)):
        p = self.part(name, mat)
        def point(r,a,z): return [center[0]+r*math.cos(a),center[1]+r*math.sin(a),center[2]+z]
        for i in range(segments):
            a, b = i*math.tau/segments,(i+1)*math.tau/segments
            for z, reverse in ((depth/2,False),(-depth/2,True)):
                points=[point(inner,a,z),point(outer,a,z),point(outer,b,z),point(inner,b,z)]
                self.face(p, list(reversed(points)) if reverse else points)
            self.face(p,[point(outer,a,-depth/2),point(outer,b,-depth/2),point(outer,b,depth/2),point(outer,a,depth/2)])
            self.face(p,[point(inner,a,depth/2),point(inner,b,depth/2),point(inner,b,-depth/2),point(inner,a,-depth/2)])
        return p

    def strap(self, name, mat, points, width, thickness=.007):
        p = self.part(name,mat)
        rings=[]
        for i,c in enumerate(points):
            t=unit(sub(points[min(i+1,len(points)-1)],points[max(0,i-1)]))
            side = [1,0,0] if abs(t[0])<.5 else unit(cross(t,[0,1,0]))
            normal=unit(cross(side,t))
            rings.append([[c[k]+width*a*side[k]/2+thickness*b*normal[k]/2 for k in range(3)] for a,b in ((-1,-1),(1,-1),(1,1),(-1,1))])
        for i in range(len(rings)-1):
            for j in range(4):
                n=(j+1)%4
                self.face(p,[rings[i][j],rings[i+1][j],rings[i+1][n],rings[i][n]])
        self.face(p,list(reversed(rings[0])))
        self.face(p,rings[-1])
        return p

    def rotate_pack(self):
        # Spine2 uses the player's frame; the pack's display face points behind the player.
        for p in self.parts:
            for key in ('vertices','normals'):
                for i in range(0,len(p[key]),3):
                    p[key][i] *= -1
                    p[key][i+2] *= -1

    def merged(self):
        result={}
        for p in self.parts:
            target=result.setdefault(p['material'],{'name':p['material'],'material':p['material'],'vertices':[],'normals':[],'uv':[],'triangles':[],'partNames':[]})
            at=len(target['vertices'])//3
            target['partNames'].append(p['name'])
            for key in ('vertices','normals','uv'): target[key].extend(round(v,7) for v in p[key])
            target['triangles'].extend(v+at for v in p['triangles'])
        return list(result.values())
