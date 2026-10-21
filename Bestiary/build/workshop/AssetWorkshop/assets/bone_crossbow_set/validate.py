"""Inspect the saved deliverables rather than just the source constants."""
from pathlib import Path
import sys,json,math
import bpy,bmesh
HERE=Path(__file__).resolve().parent; OUT=HERE/'out'
report=[]
for name,budget in [('ecp_bone_crossbow',6000),('ecp_bone_crossbow_unloaded',6000),('ecp_bone_quarrel',1200)]:
    folder=HERE.parent/name/'out'
    bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'.blend')))
    obj=bpy.data.objects[name]; mesh=obj.data; mesh.calc_loop_triangles()
    assert len(mesh.loop_triangles)<=budget
    assert len(mesh.uv_layers)==1
    assert all(math.isfinite(c) for v in mesh.vertices for c in v.co)
    bm=bmesh.new(); bm.from_mesh(mesh)
    invalid=sum(not e.is_manifold for e in bm.edges)
    zero=sum(f.calc_area()<1e-12 for f in bm.faces)
    assert invalid==0,(name,'non-manifold edges',invalid)
    assert zero==0,(name,'degenerate polygons',zero)
    assert bm.calc_volume(signed=True)>0
    bm.free()
    mat=mesh.materials[0]
    images=[n.image for n in mat.node_tree.nodes if n.type=='TEX_IMAGE']
    assert len(images)==1 and images[0].packed_file
    assert not any('reference' in (i.filepath or '').lower() for i in images)
    report.append(f'PASS {name}: {len(mesh.loop_triangles)} triangles, manifold closed parts, finite geometry, UV atlas packed.')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Gravebranch_editable.blend'))
s=bpy.context.scene; bolt=bpy.data.objects['Loaded bone quarrel']
s.frame_set(1); assert (bolt.location[1]+.075)**2<1e-8
assert abs(bolt.location[2]-.058)<1e-5
string=bpy.data.objects['drawn sinew string']; key=string.data.shape_keys.key_blocks['Released']
assert key.value==0
s.frame_set(24); assert bolt.location[1]<-2 and key.value==1
s.frame_set(90); assert abs(bolt.location[1]+.075)<1e-5 and key.value==0
report.append('PASS editable animation: seated at frame 1; projectile forward and string released at 24; reloaded at 90.')
(OUT/'blender_validation.txt').write_text('\n'.join(report)+'\n')
print('\n'.join(report))
