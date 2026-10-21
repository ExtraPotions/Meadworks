# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Small software geometry contact sheet for source inspection, without an editor run."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from mantle_geometry import cross, sub, unit

ROOT=Path(__file__).resolve().parent


def dot(a,b): return sum(x*y for x,y in zip(a,b))


def render(asset,materials):
    size=384;im=Image.new('RGB',(size,size),(35,33,30));draw=ImageDraw.Draw(im)
    eye,target=asset['icon']['position'],asset['icon']['target'];forward=unit(sub(target,eye))
    right=unit(cross(forward,[0,1,0]));up=cross(right,forward)
    light=unit([-forward[0]-.3,.9,-forward[2]+.2]);scale=size/(2*asset['icon']['orthographicSize'])
    triangles=[]
    for part in asset['parts']:
        material=materials[part['material']];color=material['color'];v=part['vertices'];normals=part['normals']
        points=[v[i:i+3] for i in range(0,len(v),3)]
        for j in range(0,len(part['triangles']),3):
            ids=part['triangles'][j:j+3];normal=normals[ids[0]*3:ids[0]*3+3]
            if dot(normal,forward)>=0: continue
            coords=[];depth=0
            for index in ids:
                p=sub(points[index],target)
                coords.append((size/2+dot(p,right)*scale,size/2-dot(p,up)*scale));depth+=dot(p,forward)
            shade=.38+.62*max(0,dot(normal,light))
            rgb=tuple(max(0,min(255,round(color[k]*255*shade+material['emission'][k]*255))) for k in range(3))
            triangles.append((depth,coords,rgb))
    for _,coords,color in sorted(triangles,reverse=True): draw.polygon(coords,fill=color)
    return im.resize((256,256),Image.Resampling.LANCZOS)


if __name__=='__main__':
    source=json.loads((ROOT/'models.json').read_text());materials={m['id']:m for m in source['materials']}
    sheet=Image.new('RGB',(256*4,290*6),(27,26,24));draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
    for i,asset in enumerate(source['assets']):
        x,y=i%4*256,i//4*290;sheet.paste(render(asset,materials),(x,y));draw.text((x+8,y+260),asset['displayName'],fill=(220,206,181),font=font)
    sheet.save(ROOT/'source-geometry-preview.png')
    print(ROOT/'source-geometry-preview.png')
