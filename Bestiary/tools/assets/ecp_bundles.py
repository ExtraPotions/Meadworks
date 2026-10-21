#!/usr/bin/env python3
# Bestiary: reads EliteCreaturesPack's GPL bundles with UnityPy. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
# SPDX-License-Identifier: GPL-3.0-or-later
"""Reads EliteCreaturesPack 0.7.2's own bundles (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow) at the pin.

Run with the UnityPy venv's python (docs/toolchain.md), isolated:

  <venv>/python -I tools/assets/ecp_bundles.py inventory       build/assets/ecp-inventory.json: every object per bundle
  <venv>/python -I tools/assets/ecp_bundles.py export-clips    build/assets/clips/<bundle>/<clip>.json: the authored
                                                               clips' serialized data; the nine clips of design 10.1
                                                               rule 1 are never read or written
  <venv>/python -I tools/assets/ecp_bundles.py export-kits     build/assets/kits/<bundle>.json: every prefab's
                                                               transform tree (names, parents, local TRS), the kit
                                                               mount offsets the Unity step needs
  <venv>/python -I tools/assets/ecp_bundles.py compare         build/assets/bundle-compare.json: every Windows bundle's
                                                               meshes and textures against the Blender outputs
  <venv>/python -I tools/assets/ecp_bundles.py compare-loot    build/assets/kraken-loot-compare.json: the Windows
                                                               kraken loot bundle against the pinned scripts' Blender
                                                               outputs in build/workshop (run workshop.py first)

Everything is written under build/ (gitignored); nothing here is shipped. The clip exports are the author's own data
(design 10.3) and the input of the Unity rebuild; the game-derived clips are excluded by name before reading.
"""
from pathlib import Path
import hashlib, json, sys

import UnityPy
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
BUNDLES_DIR = ROOT.parent / 'assets/references/source/EliteCreaturesPack-ad278ecf/EliteCreaturesPack/EliteCreaturesPack/assets/bundles'
OUT = ROOT / 'build/assets'
WORKSHOP = ROOT / 'build/workshop/AssetWorkshop'
BUNDLES = ['ecr_cryptmimic', 'ecr_slinger', 'ecr_rimegiant', 'ecp_kraken', 'ecp_kraken_loot',
           'ecp_crossbowman', 'ecp_headsman', 'ecp_skel_arsenal']
FORBIDDEN_CLIPS = {
    'ecp_xbow_idle', 'ecp_xbow_walk', 'ecp_xbow_run',
    'ecp_headsman_idle', 'ecp_headsman_walk', 'ecp_headsman_run',
    'ecp_atgeir_player_attack0', 'ecp_atgeir_player_attack1', 'ecp_atgeir_player_attack2',
}
TEXTURE_MEAN_LIMIT = 6.0     # mean absolute difference (0 to 255) still read as the same picture after BC7
LOOT = {'ecp_kraken_beak': 'ecp_kraken_beak', 'ecp_kraken_beak_shield': 'ecp_kraken_beak_shield',
        'ecp_kraken_meat': 'ecp_kraken_meat', 'ecp_kraken_meat_cooked': 'ecp_kraken_meat_cooked'}


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def load(bundle, platform='windows'):
    path = BUNDLES_DIR / f'{bundle}.{platform}'
    return UnityPy.load(str(path)), path


def name_of(obj):
    """The object's name without deserializing it (peek_name reads only the leading m_Name)."""
    try:
        return obj.peek_name() or ''
    except Exception:
        return ''


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=1, default=str) + '\n', encoding='utf-8')


def inventory():
    result = {}
    for bundle in BUNDLES:
        entry = {}
        for platform in ('windows', 'linux'):
            env, path = load(bundle, platform)
            objects = sorted(([o.type.name, name_of(o)] for o in env.objects), key=lambda x: (x[0], x[1]))
            externals = sorted({ext.path for f in env.files.values() for sub in getattr(f, 'files', {}).values()
                                for ext in getattr(sub, 'externals', [])})
            entry[platform] = {'bytes': path.stat().st_size, 'sha256': sha256(path.read_bytes()),
                               'externals': externals, 'objects': objects}
        result[bundle] = entry
        clips = [n for t, n in entry['windows']['objects'] if t == 'AnimationClip']
        print(f'{bundle}: {len(entry["windows"]["objects"])} objects, clips {clips}, externals {entry["windows"]["externals"]}')
    write_json(OUT / 'ecp-inventory.json', result)


def export_clips():
    exported, excluded = [], []
    for bundle in BUNDLES:
        for platform in ('windows', 'linux'):
            env, _ = load(bundle, platform)
            for obj in env.objects:
                if obj.type.name != 'AnimationClip':
                    continue
                name = name_of(obj)
                if name in FORBIDDEN_CLIPS:
                    excluded.append(f'{bundle}.{platform}:{name}')
                    continue
                tree = obj.read_typetree()
                target = OUT / 'clips' / bundle / platform / f'{name}.json'
                write_json(target, tree)
                exported.append({'bundle': bundle, 'platform': platform, 'clip': name,
                                 'path_id': obj.path_id, 'json_sha256': sha256(target.read_bytes())})
    write_json(OUT / 'clips/index.json', {'exported': exported, 'excluded': sorted(excluded)})
    print(f'exported {len(exported)} clip records, excluded {len(excluded)} (design 10.1 rule 1)')


def export_kits():
    for bundle in BUNDLES:
        env, _ = load(bundle)
        objects = {o.path_id: o for o in env.objects}
        transforms = {}
        for obj in env.objects:
            if obj.type.name != 'Transform':
                continue
            tree = obj.read_typetree()
            go = objects[tree['m_GameObject']['m_PathID']]
            transforms[obj.path_id] = {'name': name_of(go), 'parent': tree['m_Father']['m_PathID'],
                                       'position': tree['m_LocalPosition'], 'rotation': tree['m_LocalRotation'],
                                       'scale': tree['m_LocalScale'],
                                       'children': [c['m_PathID'] for c in tree['m_Children']]}

        def node(pid):
            t = transforms[pid]
            return {k: t[k] for k in ('name', 'position', 'rotation', 'scale')} | \
                   {'children': [node(c) for c in t['children'] if c in transforms]}

        roots = [node(pid) for pid, t in transforms.items() if t['parent'] == 0]
        write_json(OUT / 'kits' / f'{bundle}.json', sorted(roots, key=lambda r: r['name']))
        print(f'{bundle}: {len(roots)} prefab roots, {len(transforms)} transforms')


def premultiplied(image):
    """RGBA bytes with color multiplied by alpha: the color under a fully transparent texel is invisible, and the
    importer does not keep it, so only what can show is compared."""
    data = image.convert('RGBA').tobytes()
    out = bytearray(len(data))
    for i in range(0, len(data), 4):
        alpha = data[i + 3]
        out[i:i + 3] = bytes((c * alpha + 127) // 255 for c in data[i:i + 3])
        out[i + 3] = alpha
    return bytes(out)


def image_diff(a, b):
    """Raw byte identity, then mean and max absolute premultiplied channel difference (0 to 255) of two same-size
    images; None when sizes differ."""
    if a.size != b.size:
        return None
    raw = a.convert('RGBA').tobytes() == b.convert('RGBA').tobytes()
    diffs = [abs(x - y) for x, y in zip(premultiplied(a), premultiplied(b))]
    return {'mean': round(sum(diffs) / len(diffs), 3), 'max': max(diffs), 'identical': raw}


def normal_diff(ecp, own):
    """A normal map as Unity stores it (DXTnm: X in alpha, Y in green) against the PNG the Blender step wrote (X in
    red, Y in green): mean and max absolute difference over both stored axes."""
    if ecp.size != own.size:
        return None
    e, o = ecp.convert('RGBA').split(), own.convert('RGBA').split()
    pairs = list(zip(e[3].tobytes() + e[1].tobytes(), o[0].tobytes() + o[1].tobytes()))
    diffs = [abs(a - b) for a, b in pairs]
    return {'mean': round(sum(diffs) / len(diffs), 3), 'max': max(diffs), 'identical': False, 'layout': 'dxtnm'}


def compare_loot():
    env, path = load('ecp_kraken_loot')
    by_name = {}
    for obj in env.objects:
        if obj.type.name in ('Mesh', 'Texture2D'):
            by_name[(obj.type.name, name_of(obj))] = obj
    result = {'bundle': path.name, 'bundle_sha256': sha256(path.read_bytes()), 'assets': {}}
    for asset in LOOT:
        out = WORKSHOP / 'assets' / asset / 'out'
        manifest = json.loads((out / f'{asset}.json').read_text(encoding='utf-8'))
        mesh = by_name[('Mesh', asset)].read()
        triangles = sum(s.indexCount for s in mesh.m_SubMeshes) // 3
        extent = mesh.m_LocalAABB.m_Extent
        size = sorted(round(2 * v, 3) for v in (extent.x, extent.y, extent.z))
        entry = {'triangles': {'ecp': triangles, 'rebuilt': manifest['triangles']},
                 'size_m_sorted': {'ecp': size, 'rebuilt': sorted(manifest['size'])}}
        for suffix in ('albedo', 'icon'):
            texture = by_name.get(('Texture2D', f'{asset}_{suffix}'))
            own = out / f'{asset}_{suffix}.png'
            if texture is None or not own.exists():
                entry[suffix] = {'missing': [n for n, ok in (('ecp', texture is not None), ('rebuilt', own.exists())) if not ok]}
                continue
            data = texture.read()
            ecp_image = data.image
            entry[suffix] = {'ecp_format': str(data.m_TextureFormat), 'size': list(ecp_image.size),
                             'rebuilt_sha256': sha256(own.read_bytes()),
                             'diff': image_diff(ecp_image, Image.open(own))}
        size_ok = all(abs(a - b) <= 0.002 for a, b in zip(entry['size_m_sorted']['ecp'], entry['size_m_sorted']['rebuilt']))
        entry['geometry_matches'] = triangles == manifest['triangles'] and size_ok
        result['assets'][asset] = entry
        print(f'{asset}: geometry {"match" if entry["geometry_matches"] else "DIFFERS"} '
              f'(tris {triangles}/{manifest["triangles"]}, size {size}/{sorted(manifest["size"])}), '
              f'albedo {entry["albedo"].get("diff")}, icon {entry["icon"].get("diff")}')
    write_json(OUT / 'kraken-loot-compare.json', result)


def outputs_by_name():
    """Every file the Blender steps wrote (assets/*/out/**), by file name; a name seen twice keeps the first."""
    found = {}
    for file in sorted((WORKSHOP / 'assets').glob('*/out/**/*')):
        if file.is_file():
            found.setdefault(file.name, file)
    return found


def compare_all():
    """Each bundle's meshes and textures against the Blender outputs of the same name: triangles and size from the
    asset manifest, textures by premultiplied pixel difference. Objects made only in Unity (kits, cut pieces, clips,
    controllers) have no Blender output and are listed as unity_only."""
    outputs = outputs_by_name()
    result = {}
    for bundle in BUNDLES:
        env, path = load(bundle)
        entry = {'bundle_sha256': sha256(path.read_bytes()), 'meshes': {}, 'textures': {}, 'unity_only': []}
        for obj in env.objects:
            kind, name = obj.type.name, name_of(obj)
            if kind == 'Mesh':
                manifest = outputs.get(name + '.json')
                data = json.loads(manifest.read_text(encoding='utf-8')) if manifest else {}
                if 'triangles' not in data:
                    entry['unity_only'].append(f'Mesh {name}')
                    continue
                mesh = obj.read()
                triangles = sum(s.indexCount for s in mesh.m_SubMeshes) // 3
                e = mesh.m_LocalAABB.m_Extent
                size = sorted(round(2 * v, 3) for v in (e.x, e.y, e.z))
                rebuilt = sorted(data.get('size', []))
                ok = triangles == data['triangles'] and len(rebuilt) == 3 and \
                    all(abs(a - b) <= 0.002 for a, b in zip(size, rebuilt))
                entry['meshes'][name] = {'match': ok, 'triangles': [triangles, data['triangles']], 'size': [size, rebuilt]}
            elif kind == 'Texture2D':
                own = outputs.get(name + '.png')
                if own is None:
                    entry['unity_only'].append(f'Texture2D {name}')
                    continue
                data = obj.read()
                diff = normal_diff(data.image, Image.open(own)) if name.endswith('_normal') else                     image_diff(data.image, Image.open(own))
                entry['textures'][name] = {'format': str(data.m_TextureFormat), 'diff': diff,
                                           'rebuilt': own.relative_to(WORKSHOP).as_posix()}
        meshes_ok = sum(m['match'] for m in entry['meshes'].values())
        close = sum(1 for t in entry['textures'].values() if t['diff'] and t['diff']['mean'] <= TEXTURE_MEAN_LIMIT)
        print(f'{bundle}: meshes {meshes_ok}/{len(entry["meshes"])} match, textures {close}/{len(entry["textures"])} '
              f'within mean {TEXTURE_MEAN_LIMIT}, unity-only {len(entry["unity_only"])}')
        result[bundle] = entry
    write_json(OUT / 'bundle-compare.json', result)


def main(argv):
    commands = {'inventory': inventory, 'export-clips': export_clips, 'export-kits': export_kits,
                'compare-loot': compare_loot, 'compare': compare_all}
    if not argv or argv[0] not in commands:
        print(__doc__)
        return 2
    commands[argv[0]]()
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
