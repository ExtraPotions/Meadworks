"""Original low-poly mesh source. Copyright (C) 2026 expDARE.

SPDX-License-Identifier: GPL-3.0-or-later
"""
import math


def mesh(name, color, vertices, faces, material="wood", collision=False):
    triangles = []
    for face in faces:
        for i in range(1, len(face) - 1):
            triangles.extend((face[0], face[i], face[i + 1]))
    return {"name": name, "material": material, "color": color,
            "vertices": [[round(c, 6) for c in v] for v in vertices],
            "triangles": triangles, "collision": collision}


def transform(part, offset=(0, 0, 0), rotation=(0, 0, 0), scale=(1, 1, 1)):
    angles = [math.radians(a) for a in rotation]
    sx, sy, sz = [math.sin(a) for a in angles]
    cx, cy, cz = [math.cos(a) for a in angles]
    out = dict(part)
    verts = []
    for v in part["vertices"]:
        x, y, z = [v[i] * scale[i] for i in range(3)]
        y, z = y * cx - z * sx, y * sx + z * cx
        x, z = x * cy + z * sy, -x * sy + z * cy
        x, y = x * cz - y * sz, x * sz + y * cz
        verts.append([round(x + offset[0], 6), round(y + offset[1], 6), round(z + offset[2], 6)])
    out["vertices"] = verts
    return out


def box(name, color, center, size, material="wood", rotation=(0, 0, 0), collision=False):
    x, y, z = [a / 2 for a in size]
    vertices = [(-x,-y,-z), (x,-y,-z), (x,y,-z), (-x,y,-z),
                (-x,-y,z), (x,-y,z), (x,y,z), (-x,y,z)]
    faces = [(3,2,1,0), (4,5,6,7), (0,1,5,4), (7,6,2,3), (1,2,6,5), (4,7,3,0)]
    return transform(mesh(name, color, vertices, faces, material, collision), center, rotation)


def lathe(name, color, profile, segments=12, material="wood", center=(0,0,0), rotation=(0,0,0), caps=True, collision=False):
    # Profiles run bottom to top; closed vessels return through their inner wall.
    vertices = [(r * math.cos(i * math.tau / segments), y, r * math.sin(i * math.tau / segments))
                for r, y in profile for i in range(segments)]
    faces = []
    for j in range(len(profile) - 1):
        for i in range(segments):
            n = (i + 1) % segments
            faces.append((j*segments+i, (j+1)*segments+i, (j+1)*segments+n, j*segments+n))
    if caps:
        faces.extend([tuple(range(segments)), tuple((len(profile)-1)*segments+i for i in reversed(range(segments)))])
    return transform(mesh(name, color, vertices, faces, material, collision), center, rotation)


def extrude(name, color, outline, depth, material="wood", center=(0,0,0), rotation=(0,0,0), collision=False):
    # Ear clipping keeps concave carved token silhouettes intact.
    area=sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))
    if area < 0: outline = list(reversed(outline))
    n = len(outline)
    vertices = [(x,y,z) for z in (-depth/2, depth/2) for x,y in outline]
    cross=lambda a,b,c:(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    remaining=list(range(n))
    faces=[]
    while len(remaining)>3:
        found=False
        for i,b in enumerate(remaining):
            a,c=remaining[i-1],remaining[(i+1)%len(remaining)]
            if cross(outline[a],outline[b],outline[c])<1e-10: continue
            inside=False
            for p in remaining:
                if p in (a,b,c): continue
                if all(cross(outline[q],outline[r],outline[p])>=-1e-10 for q,r in ((a,b),(b,c),(c,a))):
                    inside=True; break
            if inside: continue
            faces.extend([(c,b,a),(a+n,b+n,c+n)])
            remaining.pop(i); found=True; break
        assert found,(name,"non-simple polygon")
    a,b,c=remaining
    faces.extend([(c,b,a),(a+n,b+n,c+n)])
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return transform(mesh(name, color, vertices, faces, material, collision), center, rotation)


def tube(name, color, path, radius, segments=6, material="wood", closed=False):
    vertices = []
    for i, p in enumerate(path):
        a, b = path[max(0,i-1)], path[min(len(path)-1,i+1)]
        tangent = [b[j]-a[j] for j in range(3)]
        length = math.sqrt(sum(v*v for v in tangent)) or 1
        t = [v/length for v in tangent]
        axis = [0,1,0] if abs(t[1]) < 0.9 else [1,0,0]
        u = [t[1]*axis[2]-t[2]*axis[1],t[2]*axis[0]-t[0]*axis[2],t[0]*axis[1]-t[1]*axis[0]]
        length = math.sqrt(sum(v*v for v in u))
        u = [v/length for v in u]
        v = [t[1]*u[2]-t[2]*u[1], t[2]*u[0]-t[0]*u[2], t[0]*u[1]-t[1]*u[0]]
        for j in range(segments):
            angle = j*math.tau/segments
            vertices.append([p[k]+radius*(u[k]*math.cos(angle)+v[k]*math.sin(angle)) for k in range(3)])
    faces=[]
    for i in range(len(path) if closed else len(path)-1):
        for j in range(segments):
            nxt=(i+1)%len(path)
            faces.append((i*segments+j,i*segments+(j+1)%segments,nxt*segments+(j+1)%segments,nxt*segments+j))
    if not closed:
        faces.extend([tuple(reversed(range(segments))), tuple((len(path)-1)*segments+j for j in range(segments))])
    return mesh(name,color,vertices,faces,material)


def sphere(name,color,center,size,segments=10,rings=5,material="food"):
    profile=[(max(0.04, math.sin(math.pi*i/rings)), -math.cos(math.pi*i/rings)) for i in range(rings+1)]
    part=lathe(name,color,profile,segments,material)
    return transform(part,center,scale=tuple(s/2 for s in size))


def validate(parts):
    count=0
    for part in parts:
        vertices=part["vertices"]
        assert vertices and len(part["triangles"])%3==0,part["name"]
        assert all(math.isfinite(c) for v in vertices for c in v),part["name"]
        for i in part["triangles"]: assert 0<=i<len(vertices),part["name"]
        for start in range(0,len(part["triangles"]),3):
            a,b,c=[vertices[part["triangles"][start+j]] for j in range(3)]
            u=[b[j]-a[j] for j in range(3)]; v=[c[j]-a[j] for j in range(3)]
            cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
            assert sum(x*x for x in cross)>1e-17,(part["name"],start)
        count+=len(part["triangles"])//3
    return count
