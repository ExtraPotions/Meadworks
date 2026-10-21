"""Render actual vanilla meshes and textures for local visual review."""
import sys, json, re
from pathlib import Path
import bpy
from mathutils import Vector
HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE.parents[1]/'blender'))
from workshop import reference, scene, preview
OUT=HERE/'out/reference'
index=json.loads((OUT/'guid_index.json').read_text())
rows=json.loads((OUT/'inventory.json').read_text())
names=['BoneFragments','WitheredBone','ShieldBoneTower','Wishbone','BoltBone','HardAntler','WolfFang','CharredBone','BonemawSerpentTooth','AsksvinCarrionSkull','SpearWolfFang','CrossbowArbalest','BowSpineSnap','StaffSkeleton','SledgeStagbreaker','TurretBoltBone']
paths=[]
for name in names:
    scene.clear()
    row=next(r for r in rows if r['name']==name)
    candidates=[p for p in row['materials'] if not p.startswith('Effects') and 'Glow' not in p]
    mat=None
    for path in candidates:
        text=(Path(reference.ROOT)/path).read_text()
        m=re.search(r'_MainTex:\s+m_Texture: \{fileID: \d+, guid: (\w+)',text)
        if m and m[1] in index:
            mat=reference.material(name,index[m[1]])
            break
    obj=reference.mesh(row['meshes'][0],name,mat)
    low,high=scene.bounds([obj]); size=max(high-low)
    for v in obj.data.vertices: v.co=(v.co-(low+high)/2)/size
    # References shown normalized, not claiming prefab-relative scale.
    preview._stage(obj); camera=preview._camera()
    path=str(OUT/(name+'.png'))
    preview._render(camera,[obj],Vector((.8,-1,.7)),True,path)
    paths.append(path)
preview.grid(paths,4,str(OUT/'vanilla_meshes.png'))
