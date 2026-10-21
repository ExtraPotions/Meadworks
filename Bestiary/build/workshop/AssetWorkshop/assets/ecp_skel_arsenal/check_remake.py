"""Check rebuilt exports, hold constants, bow attachment points and texture budgets."""
import ast
import json
import math
from pathlib import Path
import bpy

HERE=Path(__file__).resolve().parent
ROOT=HERE.parent
OUT=HERE/'out/remake'
BEFORE=json.loads((OUT/'before.json').read_text())
CONTRACTS={'HOLD','BLADE_SIDE','STRING','LIMBS','TOP','TIPS','NUT','MUZZLE','REST',
           'SUPPORT','TIP_S','TIP_X','LENGTH','BUTT','FOOT','HALF','SHAFT_END','NECK',
           'GRIP','TIP','HEAD','HEAD_R','GUARD_FRONT','GUARD_R','LIMB','CORE','SAG'}


def constants(path):
    found={}
    for node in ast.parse(path.read_text(encoding='utf-8-sig')).body:
        if isinstance(node,ast.Assign):
            names=set()
            for target in node.targets:
                names.update(n.id for n in ast.walk(target) if isinstance(n,ast.Name))
            if names & CONTRACTS:
                found[','.join(sorted(names))]=ast.dump(node.value)
    return found


results=[]
for name,old in BEFORE.items():
    folder=ROOT/name
    manifest=json.loads((folder/'out'/f'{name}.json').read_text())
    assert (folder/'out'/manifest['fbx']).stat().st_size > 1000
    before_constants=constants(OUT/'before'/name/'model.py')
    after_constants=constants(folder/'model.py')
    assert before_constants == after_constants, (name,'attachment constants changed')
    bpy.ops.wm.open_mainfile(filepath=str(folder/'out'/f'{name}.blend'))
    obj=bpy.data.objects[name]
    obj.data.calc_loop_triangles()
    tris=len(obj.data.loop_triangles)
    assert tris == manifest['triangles'] and tris < old['triangles'], (name,tris)
    assert len(obj.data.uv_layers)>0
    assert len(obj.data.materials)==1
    assert all(math.isfinite(c) for v in obj.data.vertices for c in v.co)
    image=bpy.data.images.load(str(folder/'out'/manifest['albedo']),check_existing=True)
    assert tuple(image.size)==(manifest['textureSize'],)*2
    assert manifest['textureSize']<=128
    for points in (folder/'out').glob('*_points.json'):
        old_points=OUT/'before'/name/points.name
        if old_points.exists():
            assert json.loads(old_points.read_text())==json.loads(points.read_text()), (name,'string points changed')
    for mat in obj.data.materials:
        for node in mat.node_tree.nodes:
            if node.type=='TEX_IMAGE':
                node.image.pack()
                node.interpolation='Closest'
    # Pack textures for portable individual review files too.
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/'out'/f'{name}.blend'))
    result={'asset':name,'before':old['triangles'],'after':tris,
            'reduction_percent':round(100*(1-tris/old['triangles']),1),
            'texture_size':manifest['textureSize'],'size':manifest['size'],
            'hold_constants_unchanged':True}
    results.append(result)
(OUT/'validation.json').write_text(json.dumps(results,indent=2))
print(json.dumps(results,indent=2))
