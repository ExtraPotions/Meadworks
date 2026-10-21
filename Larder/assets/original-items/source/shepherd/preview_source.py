"""Lightweight geometry-only debug preview. Copyright (C) 2026 expDARE.

SPDX-License-Identifier: GPL-3.0-or-later
"""
import json
import math
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont


def dot(a,b): return sum(x*y for x,y in zip(a,b))
def norm(a):
    length=math.sqrt(dot(a,a))
    return [x/length for x in a]
def cross(a,b): return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def sub(a,b): return [a[i]-b[i] for i in range(3)]


def render(asset,materials,size=320):
    canvas=Image.new("RGB",(size,size),(34,32,29))
    draw=ImageDraw.Draw(canvas)
    eye=asset["icon"]["position"]; target=asset["icon"]["target"]
    forward=norm(sub(target,eye)); right=norm(cross(forward,[0,1,0])); up=cross(right,forward)
    scale=size/(asset["icon"]["orthographicSize"]*2)
    light=norm([-.65,1,.85]); faces=[]
    for part in asset["parts"]:
        points=[part["vertices"][i:i+3] for i in range(0,len(part["vertices"]),3)]
        base=materials[part["material"]]["color"]
        for i in range(0,len(part["triangles"]),3):
            a,b,c=[points[j] for j in part["triangles"][i:i+3]]
            normal=norm(cross(sub(b,a),sub(c,a)))
            if dot(normal,forward)>=0: continue
            shade=.37+.63*max(0,dot(normal,light))
            shade+=.035*math.sin(i*1.7)
            color=tuple(min(255,max(0,round(255*(channel**.75)*shade))) for channel in base[:3])
            polygon=[]
            for p in (a,b,c):
                delta=sub(p,target)
                polygon.append((size/2+dot(delta,right)*scale,size/2-dot(delta,up)*scale))
            faces.append((sum(dot(sub(p,eye),forward) for p in (a,b,c))/3,polygon,color))
    for _,polygon,color in sorted(faces,reverse=True): draw.polygon(polygon,fill=color)
    return canvas


if __name__=="__main__":
    source=Path(sys.argv[1]); data=json.loads(source.read_text())
    materials={m["id"]:m for m in data["materials"]}
    columns=3 if len(data["assets"])==3 else 4
    rows=math.ceil(len(data["assets"])/columns)
    sheet=Image.new("RGB",(columns*320,rows*355),(27,26,24)); draw=ImageDraw.Draw(sheet)
    for i,asset in enumerate(data["assets"]):
        x=(i%columns)*320; y=(i//columns)*355
        sheet.paste(render(asset,materials),(x,y))
        draw.text((x+12,y+323),asset["displayName"],fill=(220,205,177))
    target=source.with_name("geometry-preview.png"); sheet.save(target)
    print(target)
