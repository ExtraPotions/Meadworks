# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
import json
import math
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from geometry import transform


ROOT=Path(__file__).resolve().parent


def dot(a,b):return sum(x*y for x,y in zip(a,b))
def sub(a,b):return [x-y for x,y in zip(a,b)]
def cross(a,b):return [a[1]*b[2]-a[2]*b[1],a[2]*b[0]-a[0]*b[2],a[0]*b[1]-a[1]*b[0]]
def unit(a):
    length=math.sqrt(dot(a,a))
    return [v/length for v in a]


def render(asset,materials,size=220):
    camera=unit(sub(asset['icon']['position'],asset['icon']['target']))
    forward=[-v for v in camera]
    right=unit(cross([0,1,0],forward));up=cross(forward,right)
    light=unit([-.4,.75,.8]);scale=size/(2*asset['icon']['orthographicSize'])
    image=Image.new('RGB',(size,size),(35,33,30));pixels=image.load();depth=[-999.]*size*size
    for part in asset['parts']:
        flat=part['vertices'];points=[flat[i:i+3] for i in range(0,len(flat),3)]
        if part.get('dynamic'):
            points=transform(dict(part,vertices=points),part['position'],part['rotation'],part['scale'])['vertices']
        material=materials[part['material']]
        for j in range(0,len(part['triangles']),3):
            tri=[points[k] for k in part['triangles'][j:j+3]]
            normal=unit(cross(sub(tri[1],tri[0]),sub(tri[2],tri[0])))
            if dot(normal,camera)<=0:continue
            pts=[(size/2+dot(sub(v,asset['icon']['target']),right)*scale,
                  size/2-dot(sub(v,asset['icon']['target']),up)*scale,dot(v,camera)) for v in tri]
            (x0,y0,z0),(x1,y1,z1),(x2,y2,z2)=pts
            denominator=(y1-y2)*(x0-x2)+(x2-x1)*(y0-y2)
            if abs(denominator)<1e-12:continue
            shade=.4+.6*max(0,dot(normal,light))
            color=tuple(min(255,max(0,int((c*shade+e*.6)*255))) for c,e in zip(material['color'][:3],material['emission']))
            for y in range(max(0,int(min(y0,y1,y2))),min(size,int(max(y0,y1,y2))+1)):
                for x in range(max(0,int(min(x0,x1,x2))),min(size,int(max(x0,x1,x2))+1)):
                    px,py=x+.5,y+.5
                    u=((y1-y2)*(px-x2)+(x2-x1)*(py-y2))/denominator
                    v=((y2-y0)*(px-x2)+(x0-x2)*(py-y2))/denominator
                    w=1-u-v
                    if min(u,v,w)<0:continue
                    z=u*z0+v*z1+w*z2;index=y*size+x
                    if z>depth[index]:depth[index]=z;pixels[x,y]=color
    return image.resize((256,256),Image.Resampling.LANCZOS)


def main():
    source=json.loads((ROOT/'models.json').read_text())
    materials={material['id']:material for material in source['materials']}
    columns=5;rows=math.ceil(len(source['assets'])/columns)
    sheet=Image.new('RGB',(columns*256,rows*290),(27,26,24));draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
    for i,asset in enumerate(source['assets']):
        x,y=i%columns*256,i//columns*290
        sheet.paste(render(asset,materials),(x,y))
        draw.text((x+8,y+260),asset['displayName'],fill=(220,206,181),font=font)
    path=ROOT/'source-depth-preview.png';sheet.save(path);print(path)


if __name__=='__main__':main()
