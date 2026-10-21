"""Validate baked motion, dependencies, and all eight weapon exports before delivery."""
import json
import struct
from pathlib import Path
import numpy as np
import bpy

here = Path(__file__).resolve().parent
folder = here / 'out' / 'blender'
lineup = json.loads((folder / 'arsenal.json').read_text())
report = {'weapons': [], 'animations': []}
for kind in ('dagger', 'mace', 'spear', 'atgeir', 'sword', 'axe', 'bow', 'arrow'):
    name = 'ecp_skel_' + kind
    base = here.parent / name / 'out'
    manifest = json.loads((base / (name + '.json')).read_text())
    for key in ('fbx', 'albedo', 'normal'):
        assert (base / manifest[key]).is_file(), (name, key)
    report['weapons'].append({'asset': name, 'triangles': manifest['triangles'], 'texture': manifest['textureSize']})
for group in lineup['groups']:
    sub = folder / group['folder']
    data = json.loads((sub / 'scene.json').read_text())
    motions = {}
    for part in data['parts']:
        cache = sub / part['cache']
        with cache.open('rb') as handle:
            signature, version, count, start, step, frames = struct.unpack('<12siiffi', handle.read(32))
            assert signature == b'POINTCACHE2\x00' and version == 1
            points = np.frombuffer(handle.read(), dtype='<f4').reshape(frames, count, 3)
        assert frames == group['frames'] and count * 3 == len(part['vertices'])
        assert np.isfinite(points).all()
        motion = float(np.max(np.linalg.norm(points - points[0], axis=2)))
        motions[part['name']] = round(motion, 4)
        if part['texture']:
            assert Path(part['texture']).is_file(), part['texture']
    assert max(v for k, v in motions.items() if 'Skeleton' in k) > 0.1, group['label']
    assert max(v for k, v in motions.items() if group['weapon'] in k) > 0.1, group['label']
    report['animations'].append({'label': group['label'], 'frames': group['frames'], 'motion_metres': motions})
bow = next(g for g in lineup['groups'] if g['label'] == 'Bow')
assert bow['attack'] < bow['loose'] < bow['frames']
for obj in bpy.context.scene.objects:
    for mod in obj.modifiers:
        if mod.type == 'MESH_CACHE':
            assert Path(bpy.path.abspath(mod.filepath)).is_file(), mod.filepath
report['status'] = 'passed'
(here / 'out' / 'validation.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('WORKSHOP validation passed: eight assets, seven moving skeletons and weapons, bow release, cache and texture dependencies')
