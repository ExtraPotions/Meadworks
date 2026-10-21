"""Focused structural checks for the three trough shells. Copyright 2026 expDARE.

SPDX-License-Identifier: GPL-3.0-or-later
"""
import hashlib
import json
import math
from pathlib import Path


def projected_inside(point, a, b, c):
    x,z=point
    cross=lambda p,q:(q[0]-p[0])*(z-p[2])-(q[2]-p[2])*(x-p[0])
    values=[cross(a,b),cross(b,c),cross(c,a)]
    return all(v>=-1e-8 for v in values) or all(v<=1e-8 for v in values)


def hits(part,point,minimum):
    vertices=[part["vertices"][i:i+3] for i in range(0,len(part["vertices"]),3)]
    for i in range(0,len(part["triangles"]),3):
        a,b,c=[vertices[j] for j in part["triangles"][i:i+3]]
        normal=[(b[1]-a[1])*(c[2]-a[2])-(b[2]-a[2])*(c[1]-a[1]),
                (b[2]-a[2])*(c[0]-a[0])-(b[0]-a[0])*(c[2]-a[2]),
                (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])]
        if abs(normal[1])<1e-10 or not projected_inside(point,a,b,c): continue
        y=a[1]-(normal[0]*(point[0]-a[0])+normal[2]*(point[1]-a[2]))/normal[1]
        if y>minimum: yield (part["name"],y)


def check(path):
    data=json.loads(path.read_text())
    assert data["schema"]==1 and data["units"]=="meters"
    assert len(data["assets"])==3
    receipts=[]
    for asset in data["assets"]:
        area=asset["attachment"]["foodSurface"]
        cx,cy,cz=area["center"]; sx,_,sz=area["size"]
        assert sum(p["collidable"] for p in asset["parts"])>=5
        assert all(p["state"]=="always" for p in asset["parts"])
        for x,z in [(cx,cz),(cx-sx*.25,cz),(cx+sx*.25,cz),(cx,cz-sz*.25),(cx,cz+sz*.25)]:
            obstructions=[hit for p in asset["parts"] for hit in hits(p,(x,z),cy+.015)]
            assert not obstructions,(asset["id"],x,z,obstructions)
        assert asset["attachment"]["frontRimHeight"]<=.52
        assert cy<asset["attachment"]["frontRimHeight"]
        receipts.append({"asset":asset["id"],"clearBasinVerticalRays":5,"collidableShellParts":sum(p["collidable"] for p in asset["parts"]),
                         "frontRimHeight":asset["attachment"]["frontRimHeight"],"foodSurfaceHeight":cy})
    return {"sourceSha256":hashlib.sha256(path.read_bytes()).hexdigest().upper(),"checks":receipts,
            "limit":"Structural source checks only. Physics interaction and animal access require native runtime verification."}


if __name__=="__main__":
    path=Path(__file__).with_name("models.json")
    receipt=check(path)
    path.with_name("SOURCE-CHECKS.json").write_text(json.dumps(receipt,indent=2)+"\n")
    print("PASS: 3 hollow troughs, 15 unobstructed basin rays, low front rims, explicit food anchors")
