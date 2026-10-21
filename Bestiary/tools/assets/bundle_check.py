#!/usr/bin/env python3
# Bestiary: content check of the Unity-built bundles. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
# SPDX-License-Identifier: GPL-3.0-or-later
"""Checks Bestiary's built bundles (build/bundles/<bundle>/<platform>.bundle) by content, with UnityPy, and writes the
ledger record assets/bestiary/records/bundles.json.

  <venv>/python -I tools/assets/bundle_check.py      (docs/toolchain.md; run from Bestiary/)

Per bundle and platform: bytes and SHA256; external references (only Resources/unity_builtin_extra allowed); no clip
named after one of the nine game-derived clips (design 10.1 rule 1); every mesh against its Blender manifest
(triangles, size) and against EliteCreaturesPack's bundle of the same name (triangles, size); every texture against
ECP's (present, same size); every clip against its curve record (clip_curves.py: same length, same values sampled at
30 Hz) and against ECP's clip of the same name (same length, the Q3 re-keyed ones aside); every kit tree against the
transform tree exported from ECP's bundle; renderer counts. Exit status 1 when any check fails.
"""
from pathlib import Path
import hashlib, json, sys

import UnityPy

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parent))
import clip_curves  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
BUILT = ROOT / 'build/bundles'
ECP = ROOT.parent / 'assets/references/source/EliteCreaturesPack-ad278ecf/EliteCreaturesPack/EliteCreaturesPack/assets/bundles'
WORKSHOP = ROOT / 'build/workshop/AssetWorkshop'
CURVES = ROOT / 'build/assets/curves'
KITS = ROOT / 'build/assets/kits'
RECORD = ROOT / 'assets/bestiary/records/bundles.json'
BUNDLES = ['ecp_kraken_loot', 'ecp_skel_arsenal', 'ecp_kraken', 'ecr_cryptmimic', 'ecr_slinger', 'ecp_crossbowman',
           'ecp_headsman', 'ecr_rimegiant']
PENDING = {}
SOURCES = {
    'ecp_kraken_loot': ['assets/kraken_beak_set/present.py', 'assets/kraken_food/present.py',
                        'unity/Assets/Editor/{BundleBuild,PrefabBuilder,ImportSettings,AssetManifest}.cs'],
    'ecp_skel_arsenal': ['blender/run.py on assets/ecp_skel_* and ecp_spine', 'assets/ecp_spine/icon.py',
                         'unity/Assets/Editor/{PrefabBuilder,ImportSettings,AssetManifest}.cs'],
    'ecp_kraken': ['assets/ecp_kraken/{textures.py,kraken.py}', 'unity/Assets/Editor/Kraken/*.cs (KrakenBuild steps)'],
    'ecr_cryptmimic': ['assets/crypt_mimic/export_unity.py (via tools/assets/no_reference.py)',
                       'unity/Assets/Editor/Creature/{CreatureImport,CreatureAnimator,CreaturePrefab,RootMotionCheck}.cs'],
    'ecr_slinger': ['blender/run.py on assets/ecr_slingshot, ecr_sling_band, ecr_sling_pouch, ecr_slinger_satchel',
                    'ECP bundle export: kit tree, ecr_sling_shot (as authored)'],
    'ecp_crossbowman': ['blender/run.py on assets/ecp_xbow_*', 'ECP bundle export: kit trees, four authored clips (Q3 '
                        're-key on aim and fire)'],
    'ecp_headsman': ['blender/run.py on assets/ecp_bone_greataxe', 'assets/ecp_headsman/icons.py',
                     'unity/Assets/Editor/Headsman/{HeadsmanAxe,HeadsmanPieces}.cs',
                     'ECP bundle export: kit trees, seven authored clips (Q3 re-key on the six attacks)'],
    'ecr_rimegiant': ['blender/run.py on assets/ecr_rime_*',
                     'runtime Troll numeric measurements in assets/bestiary/records/rimegiant-fit.json',
                     'unity/Assets/Editor/RimeGiant/RimeKit.Flatten, BestiaryRime numerical local TRS adapter'],
}
BESTIARY_TOOLS = ['tools/assets/workshop.py', 'tools/assets/unity_build.py', 'tools/assets/clip_curves.py',
                  'tools/assets/unity/Assets/Editor/Bestiary/*.cs']
SIZE_TOLERANCE = 0.002
CLIP_TOLERANCE = 0.002


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def name_of(obj):
    try:
        return obj.peek_name() or ''
    except Exception:
        return ''


def mesh_info(obj):
    mesh = obj.read()
    triangles = sum(s.indexCount for s in mesh.m_SubMeshes) // 3
    e = mesh.m_LocalAABB.m_Extent
    return triangles, sorted(round(2 * v, 3) for v in (e.x, e.y, e.z))


def by_type(env):
    found = {}
    for obj in env.objects:
        found.setdefault(obj.type.name, {}).setdefault(name_of(obj), []).append(obj)
    return found


def manifests():
    found = {}
    for file in sorted((WORKSHOP / 'assets').glob('*/out/*.json')):
        try:
            data = json.loads(file.read_text(encoding='utf-8'))
        except ValueError:
            continue
        if isinstance(data, dict) and 'triangles' in data and 'size' in data:
            found.setdefault(file.stem, data)
    return found


def trees(env):
    """Every prefab root's transform tree: (path, position, rotation, scale), in a stable order."""
    objects = {o.path_id: o for o in env.objects}
    nodes = {}
    for obj in env.objects:
        if obj.type.name != 'Transform':
            continue
        t = obj.read_typetree()
        nodes[obj.path_id] = (name_of(objects[t['m_GameObject']['m_PathID']]), t)
    out = {}

    def walk(pid, path, rows):
        name, t = nodes[pid]
        here = f'{path}/{name}' if path else name
        rows.append((here, [t['m_LocalPosition'][k] for k in 'xyz'], [t['m_LocalRotation'][k] for k in 'xyzw'],
                     [t['m_LocalScale'][k] for k in 'xyz']))
        for child in t['m_Children']:
            if child['m_PathID'] in nodes:
                walk(child['m_PathID'], here, rows)

    for pid, (name, t) in nodes.items():
        if t['m_Father']['m_PathID'] == 0:
            rows = []
            walk(pid, '', rows)
            out[name] = sorted(rows)
    return out


def tree_diff(a, b):
    if [r[0] for r in a] != [r[0] for r in b]:
        return 'paths differ'
    worst = 0.0
    for ra, rb in zip(a, b):
        for va, vb in zip(ra[1:], rb[1:]):
            worst = max(worst, max(abs(x - y) for x, y in zip(va, vb)))
    return worst


def clip_check(tree, bundle):
    name = tree['m_Name']
    stop = tree['m_MuscleClip']['m_StopTime']
    bindings = tree['m_ClipBindingConstant']['genericBindings']
    entry = {'length': round(stop, 4), 'bindings': len(bindings), 'events': len(tree['m_Events'])}
    record = CURVES / bundle / f'{name}.json'
    if not record.exists():
        return entry, []    # built from its own FBX (the mimic's generic clips), not from a curve record
    built = clip_curves.curves_of(tree) if bindings else []
    source = json.loads(record.read_text(encoding='utf-8'))
    problems = []
    curves = {c['attribute']: [c['keys'][i:i + 4] for i in range(0, len(c['keys']), 4)] for c in source['curves']}
    source_stop = source['settings']['stopTime'] if source['curves'] else stop
    entry['record_length'] = round(source_stop, 4)
    entry['neutral_rekey'] = source.get('neutral')
    if abs(source_stop - stop) > 1e-3:
        problems.append(f'{name}: length {stop} differs from its record {source_stop}')
    built_by = {c['attribute']: c['keys'] for c in built}
    worst = 0.0
    for attribute, keys in curves.items():
        if attribute not in built_by:
            # Unity drops a curve that equals the humanoid default it would otherwise produce: compare with 0 there.
            keys_b = [[0.0, 0.0, 0.0, 0.0]]
        else:
            keys_b = built_by[attribute]
        steps = max(1, int(round(source_stop * 30)))
        for s in range(steps + 1):
            t = min(source_stop, s / 30)
            worst = max(worst, abs(clip_curves.evaluate(keys, t) - clip_curves.evaluate(keys_b, t)))
    entry['max_value_difference'] = round(worst, 5)
    if worst > CLIP_TOLERANCE:
        problems.append(f'{name}: values differ from its record by up to {worst:.4f}')
    return entry, problems


def check(bundle, platform, meta):
    path = BUILT / bundle / f'{platform}.bundle'
    data = path.read_bytes()
    env = UnityPy.load(str(path))
    ecp_env = UnityPy.load(str(ECP / f'{bundle}.windows'))
    mine, theirs = by_type(env), by_type(ecp_env)
    problems = []
    externals = sorted({ext.path for f in env.files.values() for sub in getattr(f, 'files', {}).values()
                        for ext in getattr(sub, 'externals', [])})
    if any(e != 'Resources/unity_builtin_extra' for e in externals):
        problems.append(f'external references {externals}')
    clip_names = sorted(mine.get('AnimationClip', {}))
    forbidden = sorted(set(clip_names) & clip_curves.FORBIDDEN_CLIPS)
    if forbidden:
        problems.append(f'forbidden clips {forbidden}')
    entry = {'bytes': len(data), 'sha256': sha256(data), 'externals': externals, 'forbidden_clips_found': forbidden,
             'meshes': {}, 'textures': {}, 'clips': {}, 'kits': {}}
    for name, objs in sorted(mine.get('Mesh', {}).items()):
        triangles, size = mesh_info(objs[0])
        m = {'triangles': triangles, 'size': size}
        if name in meta:
            rebuilt = sorted(meta[name]['size'])
            m['blender'] = triangles == meta[name]['triangles'] and \
                all(abs(a - b) <= SIZE_TOLERANCE for a, b in zip(size, rebuilt))
            if not m['blender']:
                problems.append(f'mesh {name}: {triangles} tris {size}, Blender {meta[name]["triangles"]} {rebuilt}')
        elif bundle == 'ecr_rimegiant':
            problems.append(f'mesh {name}: no Blender source manifest for runtime-measured fit')
        if name in theirs.get('Mesh', {}):
            t2, s2 = mesh_info(theirs['Mesh'][name][0])
            m['ecp'] = triangles == t2 and all(abs(a - b) <= SIZE_TOLERANCE for a, b in zip(size, s2))
            if not m['ecp'] and bundle != 'ecr_rimegiant':
                problems.append(f'mesh {name}: {triangles} tris {size}, ECP {t2} {s2}')
            if bundle == 'ecr_rimegiant':
                m['ecp_comparison'] = 'informational: armor rebuilt against the recorded live-game fit, Blender is authoritative'
        entry['meshes'][name] = m
    for name, objs in sorted(mine.get('Texture2D', {}).items()):
        tex = objs[0].read()
        t = {'size': [tex.m_Width, tex.m_Height], 'format': str(tex.m_TextureFormat)}
        if name in theirs.get('Texture2D', {}):
            other = theirs['Texture2D'][name][0].read()
            t['ecp_size'] = [other.m_Width, other.m_Height]
            if t['size'] != t['ecp_size']:
                problems.append(f'texture {name}: {t["size"]}, ECP {t["ecp_size"]}')
        entry['textures'][name] = t
    for name, objs in sorted(mine.get('AnimationClip', {}).items()):
        tree = objs[0].read_typetree()
        c, p = clip_check(tree, bundle)
        if name in theirs.get('AnimationClip', {}):
            ecp_stop = theirs['AnimationClip'][name][0].read_typetree()['m_MuscleClip']['m_StopTime']
            c['ecp_length'] = round(ecp_stop, 4)
            if abs(ecp_stop - c['length']) > 1e-3:
                p.append(f'clip {name}: length {c["length"]}, ECP {ecp_stop}')
        problems += p
        entry['clips'][name] = c
    ours, ecp_trees = trees(env), trees(ecp_env)
    for root, rows in sorted(ours.items()):
        k = {'transforms': len(rows)}
        if root in ecp_trees:
            diff = tree_diff(rows, ecp_trees[root])
            k['ecp_max_trs_difference'] = diff if isinstance(diff, str) else round(diff, 6)
            if (isinstance(diff, str) or diff > 1e-4) and bundle != 'ecr_rimegiant':
                problems.append(f'kit {root}: {diff}')
        entry['kits'][root] = k
    if bundle == 'ecr_rimegiant':
        fit_path = ROOT / 'assets/bestiary/records/rimegiant-fit.json'
        mounts = json.loads(fit_path.read_text(encoding='utf-8'))['bestiary_mounts']
        expected = [('ecr_rime_kit', [0,0,0], [0,0,0,1], [1,1,1])]
        for bone in sorted({m['mount'] for m in mounts}):
            expected.append(('ecr_rime_kit/' + bone, [0,0,0], [0,0,0,1], [1,1,1]))
        for mount in mounts:
            expected.append(('ecr_rime_kit/' + mount['mount'] + '/' + mount['name'],
                             [mount['localPosition'][k] for k in 'xyz'],
                             [mount['localRotation'][k] for k in 'xyzw'],
                             [mount['localScale'][k] for k in 'xyz']))
        diff = tree_diff(ours.get('ecr_rime_kit', []), sorted(expected))
        entry['fit'] = {'sha256': sha256(fit_path.read_bytes()), 'mounts': len(mounts),
                        'max_local_trs_difference': diff if isinstance(diff,str) else round(diff,8)}
        if len(mounts) != 15 or isinstance(diff,str) or diff > 1e-5:
            problems.append(f'Rime kit differs from recorded live-game mount measurements: {diff}')
    counts = {}
    for kind in ('Mesh', 'Texture2D', 'Material', 'AnimationClip', 'Sprite', 'MeshRenderer', 'SkinnedMeshRenderer',
                 'AnimatorController', 'Avatar', 'BoxCollider', 'MeshCollider'):
        a = sum(len(v) for v in mine.get(kind, {}).values())
        b = sum(len(v) for v in theirs.get(kind, {}).values())
        if a or b:
            counts[kind] = [a, b]
    entry['counts_built_vs_ecp'] = counts
    entry['problems'] = problems
    return entry


def main():
    meta = manifests()
    record = {'workshop_pin': 'a8ea29008f23dcebfd3afe93c157c6bf6e8d7be1', 'unity': '6000.0.75f1 (26349cd2a5c8)',
              'blender': '5.2.1 LTS', 'bestiary_tools': BESTIARY_TOOLS, 'excluded_clips': sorted(clip_curves.FORBIDDEN_CLIPS),
              'pending': PENDING, 'bundles': {}}
    failed = False
    for bundle in BUNDLES:
        entry = {'sources': SOURCES[bundle], 'platforms': {}}
        report = BUILT / bundle / f'{bundle}.report.txt'
        if report.exists():
            entry['unity_report'] = [l for l in report.read_text(encoding='utf-8').splitlines()
                                     if l.startswith(('platform', 'kit', 'clip', 'animator', 'mimic', 'kraken', 'greataxe'))]
        for platform in ('windows', 'linux'):
            if not (BUILT / bundle / f'{platform}.bundle').exists():
                entry['platforms'][platform] = 'not built'
                print(f'{bundle}.{platform}: not built')
                continue
            result = check(bundle, platform, meta)
            entry['platforms'][platform] = result
            failed |= bool(result['problems'])
            print(f'{bundle}.{platform}: {result["bytes"]} bytes sha256 {result["sha256"]}; meshes {len(result["meshes"])}, '
                  f'textures {len(result["textures"])}, clips {len(result["clips"])}, kits {len(result["kits"])}; '
                  f'counts {result["counts_built_vs_ecp"]}; problems {result["problems"] or "none"}')
        record['bundles'][bundle] = entry
    RECORD.write_text(json.dumps(record, indent=1) + '\n', encoding='utf-8')
    print(f'wrote {RECORD.relative_to(ROOT).as_posix()}')
    return 1 if failed else 0


if __name__ == '__main__':
    sys.exit(main())
