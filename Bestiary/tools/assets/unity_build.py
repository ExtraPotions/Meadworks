#!/usr/bin/env python3
# Bestiary Unity bundle build driver. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
# SPDX-License-Identifier: GPL-3.0-or-later
"""Builds Bestiary's asset bundles in a Bestiary-owned Unity project (stage 2 of the rebuild, design section 10).

  python tools/assets/unity_build.py stage                       build/unity from the pinned workshop + Bestiary files
  python tools/assets/unity_build.py bundle [name ...] [--platforms windows,linux]
                                                                 stage each bundle's inputs, run Unity, collect
                                                                 build/bundles/<name>/<platform>.bundle and
                                                                 build/bundles/<name>/<name>.report.txt

The project is the pinned AssetWorkshop Unity project (`unity/Assets/Editor`, `unity/Assets/Preview`,
`unity/ProjectSettings` at a8ea290, staged by workshop.py into build/workshop) with Bestiary's package manifest (no
CoplayDev MCP git package) and Bestiary's editor scripts (tools/assets/unity/Assets/Editor/Bestiary). Inputs per bundle:
the Blender outputs of workshop.py, the authored clips as editor curves (clip_curves.py curves) and the kit transform
trees exported from EliteCreaturesPack's bundles (ecp_bundles.py export-kits). No game assets are exported. The Rime
Giant uses numerical fit.json measured transiently from a spawned Troll in the running game.

Unity: BESTIARY_UNITY, else the first of the known 6000.0.75f1 install folders (docs/toolchain.md).
"""
from pathlib import Path
import json, os, shutil, subprocess, sys

ROOT = Path(__file__).resolve().parents[2]
WORKSHOP = ROOT / 'build/workshop/AssetWorkshop'
TEMPLATE = Path(__file__).resolve().parent / 'unity'
PROJECT = ROOT / 'build/unity'
ASSETS_OUT = ROOT / 'build/assets'
BUNDLES_OUT = ROOT / 'build/bundles'
HOME = Path(os.environ.get('USERPROFILE') or Path.home())
UNITY_CANDIDATES = [
    HOME / 'Tools/Unity/Editor/6000.0.75f1/Editor/Unity.exe',
    Path('C:/Program Files/Unity 6000.0.75f1/Editor/Unity.exe'),
    Path('C:/Program Files/Unity/Hub/Editor/6000.0.75f1/Editor/Unity.exe'),
]
UNITY_VERSION = '6000.0.75f1'

ARSENAL = ['ecp_skel_dagger', 'ecp_skel_sword', 'ecp_skel_axe', 'ecp_skel_mace', 'ecp_skel_spear', 'ecp_skel_atgeir',
           'ecp_skel_bow', 'ecp_skel_bow_player', 'ecp_skel_arrow', 'ecp_spine']
LOOT = ['ecp_kraken_beak', 'ecp_kraken_beak_shield', 'ecp_kraken_meat', 'ecp_kraken_meat_cooked']
XBOW = ['ecp_xbow_crossbow', 'ecp_xbow_string', 'ecp_xbow_quiver', 'ecp_xbow_bolt']
SLINGER = ['ecr_slingshot', 'ecr_sling_band', 'ecr_sling_pouch', 'ecr_slinger_satchel']
RIME = [f'ecr_rime_plate_{i}' for i in range(8)] + ['ecr_rime_crust'] + [f'ecr_rime_crust_{i}' for i in range(1,7)] + ['ecr_rime_boulder']

# Bundle -> (entry point, part folders, extra staging). Order: plain bundles first.
BUNDLES = {
    'ecp_kraken_loot': ('Generic', LOOT, None),
    'ecp_skel_arsenal': ('Generic', ARSENAL, None),
    'ecp_kraken': ('Kraken', [], 'kraken'),
    'ecr_cryptmimic': ('Mimic', [], 'mimic'),
    'ecr_slinger': ('Slinger', SLINGER, 'kit'),
    'ecp_crossbowman': ('Crossbowman', XBOW, 'kit'),
    'ecp_headsman': ('Headsman', ['ecp_bone_greataxe'], 'headsman'),
    'ecr_rimegiant': ('RimeGiant', RIME, 'rime'),
}
PENDING = {}


def unity_path():
    if os.environ.get('BESTIARY_UNITY'):
        return Path(os.environ['BESTIARY_UNITY'])
    for candidate in UNITY_CANDIDATES:
        if candidate.exists():
            return candidate
    raise SystemExit('Unity 6000.0.75f1 not found; set BESTIARY_UNITY (docs/toolchain.md)')


def copy_tree(source, target):
    if target.exists():
        shutil.rmtree(target)
    shutil.copytree(source, target)


def stage():
    """build/unity: the pinned project's editor scripts, preview scripts and settings, Bestiary's manifest and scripts.
    An existing Library/ is kept (Unity's import cache)."""
    if not (WORKSHOP / 'unity/Assets/Editor').exists():
        raise SystemExit('workshop not staged; run: python tools/assets/workshop.py stage')
    (PROJECT / 'Assets').mkdir(parents=True, exist_ok=True)
    for name in ('Editor', 'Preview'):
        copy_tree(WORKSHOP / 'unity/Assets' / name, PROJECT / 'Assets' / name)
        shutil.copyfile(WORKSHOP / f'unity/Assets/{name}.meta', PROJECT / f'Assets/{name}.meta')
    copy_tree(WORKSHOP / 'unity/ProjectSettings', PROJECT / 'ProjectSettings')
    copy_tree(TEMPLATE / 'Packages', PROJECT / 'Packages')
    copy_tree(TEMPLATE / 'Assets/Editor/Bestiary', PROJECT / 'Assets/Editor/Bestiary')
    for gone in ('Assets/Reference', 'Assets/BundleExtras'):
        if (PROJECT / gone).exists():
            raise SystemExit(f'{gone} exists in the build project; Bestiary never uses a game export')
    print(f'staged the Unity project in {PROJECT.relative_to(ROOT).as_posix()} (workshop editor scripts at the pin, '
          f'Bestiary manifest and scripts)')


def part_files(asset):
    out = WORKSHOP / 'assets' / asset / 'out'
    manifest = json.loads((out / f'{asset}.json').read_text(encoding='utf-8'))
    names = [f'{asset}.json', manifest['fbx'], manifest['albedo']] + ([manifest['normal']] if manifest['normal'] else [])
    if (out / f'{asset}_icon.png').exists():
        names.append(f'{asset}_icon.png')
    return [out / n for n in names]


def flatten_kit(bundle):
    """ecp_bundles.py's nested trees as a flat list (parents first) with paths, for Unity's JsonUtility."""
    roots = json.loads((ASSETS_OUT / 'kits' / f'{bundle}.json').read_text(encoding='utf-8'))
    nodes = []

    def add(node, parent, path):
        here = f'{path}/{node["name"]}' if path else node['name']
        nodes.append({'name': node['name'], 'path': here, 'parent': parent,
                      'position': [node['position'][k] for k in 'xyz'],
                      'rotation': [node['rotation'][k] for k in 'xyzw'],
                      'scale': [node['scale'][k] for k in 'xyz']})
        index = len(nodes) - 1
        for child in node['children']:
            add(child, index, here)

    for root in roots:
        add(root, -1, '')
    return {'nodes': nodes}


def stage_bundle(bundle):
    _, parts, extra = BUNDLES[bundle]
    for folder in ('Assets/Bundles', 'Assets/Creatures'):
        if (PROJECT / folder).exists():
            shutil.rmtree(PROJECT / folder)
        meta = PROJECT / (folder + '.meta')
        if meta.exists():
            meta.unlink()
    target = PROJECT / 'Assets/Bundles' / bundle
    target.mkdir(parents=True)
    for asset in parts:
        (target / asset).mkdir()
        for file in part_files(asset):
            shutil.copyfile(file, target / asset / file.name)
    if extra == 'kraken':
        here = WORKSHOP / 'assets/ecp_kraken/out'
        for file in [here / 'tentacle/ecp_kraken_tentacle.json', here / 'head/ecp_kraken_head.json',
                     *(here / 'tentacle').glob('ecp_kraken_tentacle_*.png'), *(here / 'head').glob('ecp_kraken_skin_*.png'),
                     *(here / 'textures').glob('*.png')]:
            shutil.copyfile(file, target / file.name)
    if extra == 'mimic':
        shutil.rmtree(target)
        creature = PROJECT / 'Assets/Creatures/crypt_mimic'
        creature.mkdir(parents=True)
        for name in ('crypt_mimic.fbx', 'crypt_mimic.json'):
            shutil.copyfile(WORKSHOP / 'assets/crypt_mimic/out/unity' / name, creature / name)
    if extra in ('kit', 'headsman'):
        (target / '_kit.json').write_text(json.dumps(flatten_kit(bundle)) + '\n', encoding='utf-8')
        curves = ASSETS_OUT / 'curves' / bundle
        if curves.exists():
            shutil.copytree(curves, target / '_curves')
    if extra == 'headsman':
        shutil.copytree(WORKSHOP / 'assets/ecp_headsman/out/icons', target / '_icons')
    if extra == 'rime':
        shutil.copyfile(WORKSHOP / 'out/rimegiant/fit.json', target / '_fit.json')


def run_unity(bundle, platforms):
    method, _, _ = BUNDLES[bundle]
    unity = unity_path()
    out = BUNDLES_OUT / bundle
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)
    log = BUNDLES_OUT / f'{bundle}.unity.log'
    args = [str(unity), '-batchmode', '-nographics', '-quit', '-projectPath', str(PROJECT), '-logFile', str(log),
            '-executeMethod', f'Bestiary.Build.BestiaryBundles.{method}', '-bestiaryBundle', bundle,
            '-bestiaryOut', str(out), '-bestiaryPlatforms', platforms]
    code = subprocess.run(args).returncode
    text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
    for line in text.splitlines():
        if line.startswith(('BESTIARY', 'WORKSHOP')) or 'error CS' in line or 'licens' in line.lower() and 'error' in line.lower():
            print('  ' + line)
    built = collect(bundle, platforms.split(','))
    print(f'{bundle}: Unity exit {code}, built {built or "nothing"} (log {log.relative_to(ROOT).as_posix()})')
    return code == 0 and bool(built)


def collect(bundle, platforms):
    """Keeps build/bundles/<bundle>/<platform>.bundle and the report; drops Unity's own per-platform folders and file
    copies, which carry EliteCreaturesPack's bundle names (the art guard rejects any file so named)."""
    out = BUNDLES_OUT / bundle
    built = []
    for platform in platforms:
        file = out / f'{bundle}.{platform}'
        if file.exists():
            file.replace(out / f'{platform}.bundle')
        if (out / f'{platform}.bundle').exists():
            built.append(platform)
        if (out / platform).exists():
            shutil.rmtree(out / platform)
    return built


def main(argv):
    if not argv or argv[0] not in ('stage', 'bundle'):
        print(__doc__)
        return 2
    if argv[0] == 'stage':
        stage()
        return 0
    platforms = 'windows,linux'
    names = []
    rest = argv[1:]
    while rest:
        item = rest.pop(0)
        if item == '--platforms':
            platforms = rest.pop(0)
        else:
            names.append(item)
    names = names or list(BUNDLES)
    unknown = [n for n in names if n not in BUNDLES]
    if unknown:
        reasons = [f'{n}: {PENDING[n]}' if n in PENDING else f'{n}: unknown' for n in unknown]
        raise SystemExit('not buildable: ' + '; '.join(reasons))
    BUNDLES_OUT.mkdir(parents=True, exist_ok=True)
    ok = True
    for bundle in names:
        stage_bundle(bundle)
        ok &= run_unity(bundle, platforms)
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
