# Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
"""Geometry and catalog coverage checks, independent of Unity and Valheim."""
import copy
import math
import re
from pathlib import Path
from mantle_geometry import cross, sub


def covers_origin(points, axes):
    a,b,c=[[p[k] for k in axes] for p in points]
    det=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
    if abs(det)<1e-14: return False
    u=((b[1]-c[1])*(-c[0])+(c[0]-b[0])*(-c[1]))/det
    v=((c[1]-a[1])*(-c[0])+(a[0]-c[0])*(-c[1]))/det
    return min(u,v,1-u-v)>-1e-8


def validate(data):
    assert data['schema']==1 and data['product']=='Mantle' and data['units']=='meters'
    assert len(data['assets'])==21 and len({a['id'] for a in data['assets']})==21
    materials={m['id'] for m in data['materials']}
    assert len(materials)==len(data['materials'])
    root=Path(__file__).resolve().parents[4]/'src'
    catalog_paths=[root/'Mantle.Plugin/Adopted/Inventory/Backpacks/BackpackCatalog.cs',root/'Mantle.Plugin/Adopted/Inventory/Tackle/TackleboxCatalog.cs',root/'Mantle.Core/Accessories/AccessoryCatalog.cs']
    expected=set()
    for path in catalog_paths:
        text=path.read_text(encoding='utf-8');names=set(re.findall(r'"(Mantle_[A-Za-z]+)"',text))
        # A box in TackleboxCatalog.Tinted wears another box's art with a tint and has no asset of its own.
        for const in re.findall(r'\[(\w+)\] = \("Mantle_',text): names-=set(re.findall(r'const string '+const+r' = "(Mantle_[A-Za-z]+)"',text))
        expected.update(names)
    actual={p for a in data['assets'] for p in a['prefabs']}
    assert actual==expected, f'Catalog mismatch: missing {expected-actual}; surplus {actual-expected}'
    items=[]
    for a in data['assets']:
        assert all(p['state']=='always' for p in a['parts'])
        assert a['id']==a['prefabs'][0].lower()
        assert len(a['parts'])<=10, f'Material draw budget: {a["id"]}'
        assert all(p['material'] in materials for p in a['parts'])
        all_points=[];triangles=0
        for p in a['parts']:
            v,n,uv,ids=p['vertices'],p['normals'],p['uv'],p['triangles']
            assert len(v)%3==0 and len(n)==len(v) and len(uv)==len(v)//3*2
            assert all(math.isfinite(x) for x in v+n+uv)
            assert all(0<=x<=1 for x in uv)
            points=[v[i:i+3] for i in range(0,len(v),3)];all_points+=points
            assert len(ids)%3==0 and all(type(x)==int and 0<=x<len(points) for x in ids)
            volume=0
            for j in range(0,len(ids),3):
                ia,ib,ic=ids[j:j+3];va,vb,vc=[points[k] for k in (ia,ib,ic)]
                normal=cross(sub(vb,va),sub(vc,va))
                assert sum(x*x for x in normal)>1e-22, f'Degenerate: {a["id"]}/{p["name"]}'
                assert sum(normal[k]*n[ia*3+k] for k in range(3))>0, f'Winding/normal: {a["id"]}/{p["name"]}'
                volume+=sum(va[k]*cross(vb,vc)[k] for k in range(3))/6
                if a['kind']=='ring': assert not covers_origin([va,vb,vc],(0,1)), 'Ring opening was capped'
                if a['kind']=='belt': assert not covers_origin([va,vb,vc],(0,2)), 'Belt opening was capped'
            assert volume>0, f'Inward material volume: {a["id"]}/{p["name"]}'
            for j in range(0,len(n),3): assert abs(sum(x*x for x in n[j:j+3])-1)<1e-5
            triangles+=len(ids)//3
        low=[min(p[i] for p in all_points) for i in range(3)];high=[max(p[i] for p in all_points) for i in range(3)]
        assert all(abs(x-y)<1e-6 for x,y in zip(low+high,a['boundsMin']+a['boundsMax']))
        for k in range(3):
            assert a['collider']['size'][k]>0
            assert abs((low[k]+high[k])/2-a['collider']['center'][k])<1e-6
            assert abs(high[k]-low[k]-a['collider']['size'][k])<1e-6
        if a['kind']=='backpack':
            assert -.75<low[1]<-.3 and .2<high[1]<.45
            assert -.5<low[2]<-.24 and high[2]<=.021
            assert .3<high[0]-low[0]<.7
            assert a['attachment']['bone']=='Spine2' and a['attachment']['fitVerifiedInGame'] is False
        if a['kind']=='ring': assert .025<high[0]-low[0]<.04
        assert 100<triangles<6000, f'Triangle budget: {a["id"]}: {triangles}'
        items.append(dict(id=a['id'],prefab=a['prefabs'][0],triangles=triangles,materials=len(a['parts']),boundsMeters=[round(high[i]-low[i],4) for i in range(3)]))
    return dict(status='PASS',checks=['21 exact catalog IDs','finite flat mesh arrays','all triangle indices valid','no degenerate triangles','unit normals and matching winding','outward signed mesh volumes','ring and belt holes are uncapped','UV range','declared bounds and colliders match geometry','backpack Spine2 coordinate and clearance envelope','ring physical dimensions','material and triangle budgets'],
                assetCount=len(items),triangles=sum(a['triangles'] for a in items),items=items,
                limits=['No Unity build or rendered icon verification','Native material borrowing unverified','No in-game attachment/cape/weapon clipping check'])


if __name__=='__main__':
    from generate import build
    data=build();report=validate(data)
    bad=copy.deepcopy(data);bad['assets'][0]['parts'][0]['triangles'][0]=-1
    try: validate(bad)
    except AssertionError: pass
    else: raise AssertionError('Negative index admitted')
    bad=copy.deepcopy(data);bad['assets'].pop()
    try: validate(bad)
    except AssertionError: pass
    else: raise AssertionError('Missing asset admitted')
    print(f"PASS: {report['assetCount']} assets, {report['triangles']} triangles; invalid index and missing asset rejected")
