#!/usr/bin/env python3
# Bestiary asset rebuild driver. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
"""Stages EliteCreaturesPack's pinned AssetWorkshop and runs its Blender steps for Bestiary's bundles.

  python tools/assets/workshop.py stage                  extract the workshop at the pin into build/workshop
  python tools/assets/workshop.py blender [bundle ...]   run the pinned Blender steps (default: every bundle)
  python tools/assets/workshop.py hashes                 write build/assets/blender-outputs.json (SHA256 of outputs)

The workshop is EliteCreaturesPack's own asset source (GPL-3.0-or-later, Copyright (C) 2026 Gerald Glasgow), read
from the pinned checkout with `git archive` at the AssetWorkshop pin; only the WORKSHOP_EDITS text edits apply. Its `rip-reference.ps1`
(a game asset export) is never staged (design 10.1 rule 7), and no step reads a game export: a step that needs one
fails, and the bundle is reported blocked. One adaptation: the mimic's export runs through no_reference.py, which
gives its two game-textured materials placeholder images (their paths are all the export records).

Blender: BESTIARY_BLENDER, else %USERPROFILE%/Tools/blender-5.2.1-windows-x64/blender.exe.
Python for plain-Python steps (numpy, Pillow): BESTIARY_PYTHON, else the UnityPy venv's python.
"""
from pathlib import Path
import base64, hashlib, io, json, os, shutil, subprocess, sys, tarfile

ROOT = Path(__file__).resolve().parents[2]
WORKSPACE = ROOT.parent
CHECKOUT = WORKSPACE / 'assets/references/source/EliteCreaturesPack-ad278ecf'
WORKSHOP_PIN = 'a8ea29008f23dcebfd3afe93c157c6bf6e8d7be1'
STAGE = ROOT / 'build/workshop'
WORKSHOP = STAGE / 'AssetWorkshop'
OUT = ROOT / 'build/assets'
NEVER_STAGED = {'AssetWorkshop/rip-reference.ps1'}
# Reviewed text edits to staged and shipped copies. Path -> (pinned SHA256, [(upstream bytes, replacement)], shipped SHA256).
# Upstream bytes are stored base64-encoded; shipped() checks both hashes, so the edited files are unchanged.
def _b(text):
    return base64.b64decode(text)


WORKSHOP_EDITS = {
    'AssetWorkshop/assets/ecp_crossbowman/blender_compare.py': (
        '5176F4CCE0D36D9058DA18DB76302C8C76804C077116C2528FD1C05DF121B3E7',
        [(_b('X2xhYmVsKCdvdXJzIChDbGF1ZGUpJyw='), b"_label('ours',"), (_b('X2xhYmVsKCdDaGF0R1BUIChHcmF2ZWJyYW5jaCknLA=='), b"_label('Gravebranch',")],
        'A34C7129F908BEB9657A09B1BBCE7B2B723DD6C5CEDA03C402E044C11007E54A'),
    'AssetWorkshop/assets/ecp_spine/BRIEF.md': (
        '892D4A2E7A8022E727BECE65FED70170F5E968054D62B679BB94BFB50DCBFB95',
        [(_b('UkVBRE1FLCBDSEFOR0VMT0csIENMQVVERS5tZC4='), b'README, CHANGELOG.')],
        '2A79184080BC7EAD695ADA61051FA2831AA46548E057C4F59C5E739CF80035EB'),
    'AssetWorkshop/README.md': (
        '6DF2E98D854F3B0A8BFFCF7F3468FB7B216A94EF31A6854F9EE6CD4BE48ACC67',
        [(_b('Q2xhdWRlIENvZGUgY2FuIGFsc28gZHJpdmUgYW4gb3BlbiBCbGVuZGVyIG9yIFVuaXR5IHdpbmRvdw=='), b'An open Blender or Unity window can also be driven'),
         (_b('IChXb3Jrc3BhY2UgcnVsZTogYENMQVVERS5tZGAsICJBbHdheXMiLik='), b''),
         (_b('VGhlIGxvY2FsIHNraWxsIGB2YWxoZWltLWFzc2V0YAooYC5jbGF1ZGUvc2tpbGxzL2ApIGlzIHRoZSBvcmRlcg=='), b"The local skill `valheim-asset`\nis the order"),
         (_b('KHRoZSBDaGF0R1BULW1hZGUgR3JhdmVicmFuY2ggc2V0IGlu'), b'(the Gravebranch set in'),
         (_b('VGhlIG1vZCdzIGBBcnNlbmFsL2AgY29kZSAoc2VlIGl0cyBDTEFVREUubWQpIHB1dHMgdGhl'), b"The mod's `Arsenal/` code puts the")],
        'BA2D0D86D5CAB07C9293E6B9CB96278CF126AEAFDE9DA03859F379494F45C0AA'),
}


def shipped(name, data):
    """Staged/shipped bytes of a workshop file (AssetWorkshop/...): the pinned bytes with any reviewed edit applied."""
    if name not in WORKSHOP_EDITS:
        return data
    pinned, substitutions, result = WORKSHOP_EDITS[name]
    current = hashlib.sha256(data).hexdigest().upper()
    if current == result:
        return data
    if current != pinned:
        raise ValueError(f'{name}: differs from its pin; review WORKSHOP_EDITS')
    for old, new in substitutions:
        if data.count(old) != 1:
            raise ValueError(f'{name}: reviewed edit no longer applies')
        data = data.replace(old, new)
    if hashlib.sha256(data).hexdigest().upper() != result:
        raise ValueError(f'{name}: edited bytes differ from the recorded result')
    return data
HOME = Path(os.environ.get('USERPROFILE') or Path.home())
BLENDER = Path(os.environ.get('BESTIARY_BLENDER') or HOME / 'Tools/blender-5.2.1-windows-x64/blender.exe')
PYTHON = Path(os.environ.get('BESTIARY_PYTHON') or HOME / 'Tools/unitypy-venv/Scripts/python.exe')


def run_asset(name):
    return ('run', name)


def script(path, *args):
    return ('script', path, args)


def icon(asset, file):
    """An authored icon kept beside the asset's source, copied into its out folder (as the pinned build scripts do)."""
    return ('copy', f'assets/{asset}/{file}', f'assets/{asset}/out/{file}')


ARSENAL = ['ecp_skel_dagger', 'ecp_skel_sword', 'ecp_skel_axe', 'ecp_skel_mace', 'ecp_skel_spear', 'ecp_skel_atgeir',
           'ecp_skel_bow', 'ecp_skel_bow_player', 'ecp_skel_arrow', 'ecp_spine']
ARSENAL_ICONS = ['ecp_skel_dagger', 'ecp_skel_sword', 'ecp_skel_axe', 'ecp_skel_mace', 'ecp_skel_spear',
                 'ecp_skel_atgeir', 'ecp_skel_bow_player', 'ecp_skel_arrow']
XBOW = ['ecp_xbow_crossbow', 'ecp_xbow_string', 'ecp_xbow_quiver', 'ecp_xbow_bolt']
SLINGER = ['ecr_slingshot', 'ecr_sling_band', 'ecr_sling_pouch', 'ecr_slinger_satchel']
RIME = [f'ecr_rime_plate_{i}' for i in range(8)] + ['ecr_rime_crust'] + [f'ecr_rime_crust_{i}' for i in range(1, 7)] + \
       ['ecr_rime_boulder']

# Bundle -> the pinned Blender steps, in the order the pinned build scripts run them (assets/<bundle>/build.ps1).
PLAN = {
    'ecp_kraken_loot': [script('assets/kraken_beak_set/present.py'), script('assets/kraken_food/present.py')],
    'ecp_skel_arsenal': [run_asset(a) for a in ARSENAL] + [script('assets/ecp_spine/icon.py')] +
                        [icon(a, a + '_icon.png') for a in ARSENAL_ICONS],
    'ecp_crossbowman': [run_asset(a) for a in XBOW] +
                       [icon('ecp_xbow_crossbow', 'ecp_xbow_crossbow_icon.png'),
                        icon('ecp_xbow_bolt', 'ecp_xbow_bolt_icon.png')],
    'ecp_headsman': [run_asset('ecp_bone_greataxe'),
                     ('blend', 'assets/ecp_bone_greataxe/out/ecp_bone_greataxe.blend', 'assets/ecp_headsman/icons.py'),
                     ('copy', 'assets/ecp_bone_greataxe/ecp_greataxe_icon.png',
                      'assets/ecp_headsman/out/icons/ecp_greataxe_icon.png')],
    'ecr_slinger': [run_asset(a) for a in SLINGER],
    'ecp_kraken': [('python', 'assets/ecp_kraken/textures.py', ('assets/ecp_kraken/out/textures',)),
                   script('assets/ecp_kraken/kraken.py', '--part', 'tentacle', '--no-preview'),
                   script('assets/ecp_kraken/kraken.py', '--part', 'head', '--no-preview')],
    'ecr_rimegiant': [run_asset(a) for a in RIME],
    # Adaptation: the mimic's export reads two game textures only to record their paths; no_reference.py supplies
    # placeholders instead (design 10.1 rule 7), and the result is compared with the ECP bundle.
    'ecr_cryptmimic': [('noref', 'assets/crypt_mimic/export_unity.py', ())],
}


def git(*args, binary=False):
    result = subprocess.run(['git', '-C', str(CHECKOUT), *args], capture_output=True)
    if result.returncode != 0:
        raise SystemExit(f'git {" ".join(args)} failed: {result.stderr.decode(errors="replace").strip()}')
    return result.stdout if binary else result.stdout.decode()


def stage():
    """Extracts AssetWorkshop at the pin (except NEVER_STAGED) into build/workshop, replacing any earlier stage."""
    git('cat-file', '-e', WORKSHOP_PIN + '^{commit}')
    archive = git('archive', '--format=tar', WORKSHOP_PIN, 'AssetWorkshop', binary=True)
    if STAGE.exists():
        shutil.rmtree(STAGE)
    STAGE.mkdir(parents=True)
    count = 0
    with tarfile.open(fileobj=io.BytesIO(archive)) as tar:
        for member in tar.getmembers():
            if member.name in NEVER_STAGED or not (member.isfile() or member.isdir()):
                continue
            target = (STAGE / member.name).resolve()
            if STAGE.resolve() not in target.parents:
                raise SystemExit(f'archive member outside the stage: {member.name}')
            tar.extract(member, STAGE, filter='data')
            count += member.isfile()
    for name in WORKSHOP_EDITS:
        path = STAGE / name
        path.write_bytes(shipped(name, path.read_bytes()))
    (STAGE / 'PIN').write_text(WORKSHOP_PIN + '\n', encoding='ascii')
    print(f'staged {count} files of AssetWorkshop at {WORKSHOP_PIN[:7]} into {STAGE.relative_to(ROOT).as_posix()}')


def blender(*args, cwd):
    if not BLENDER.exists():
        raise SystemExit(f'Blender not found at {BLENDER} (set BESTIARY_BLENDER)')
    return subprocess.run([str(BLENDER), *args], cwd=cwd, capture_output=True, text=True, errors='replace')


def step(item, log):
    kind = item[0]
    if kind == 'run':
        result = blender('--background', '--factory-startup', '--python-exit-code', '1', '--python',
                         str(WORKSHOP / 'blender/run.py'), '--', '--asset', str(WORKSHOP / 'assets' / item[1]),
                         '--no-preview', cwd=WORKSHOP)
    elif kind == 'script':
        result = blender('--background', '--factory-startup', '--python-exit-code', '1', '--python',
                         str(WORKSHOP / item[1]), '--', *item[2], cwd=WORKSHOP)
    elif kind == 'noref':
        result = blender('--background', '--factory-startup', '--python-exit-code', '1', '--python',
                         str(Path(__file__).with_name('no_reference.py')), '--', str(WORKSHOP / item[1]), *item[2],
                         cwd=WORKSHOP)
    elif kind == 'blend':
        result = blender('--background', str(WORKSHOP / item[1]), '--python-exit-code', '1', '--python',
                         str(WORKSHOP / item[2]), cwd=WORKSHOP)
    elif kind == 'python':
        result = subprocess.run([str(PYTHON), '-I', str(WORKSHOP / item[1]), *[str(WORKSHOP / a) for a in item[2]]],
                                cwd=WORKSHOP, capture_output=True, text=True, errors='replace')
    elif kind == 'copy':
        (WORKSHOP / item[2]).parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(WORKSHOP / item[1], WORKSHOP / item[2])
        return True, f'copied {item[1]}'
    else:
        raise ValueError(kind)
    log.write(f'### {item}\n{result.stdout}\n{result.stderr}\n')
    tail = [line for line in (result.stdout + result.stderr).splitlines()
            if line.startswith(('Traceback', 'FileNotFoundError', 'OSError', 'RuntimeError', 'Error'))]
    return result.returncode == 0, (tail[-1] if tail else f'exit {result.returncode}')


def run_blender(bundles):
    if not (WORKSHOP / 'blender/run.py').exists():
        raise SystemExit('workshop not staged; run: python tools/assets/workshop.py stage')
    OUT.mkdir(parents=True, exist_ok=True)
    report = json.loads((OUT / 'blender-report.json').read_text()) if (OUT / 'blender-report.json').exists() else {}
    for bundle in bundles:
        ok_all, steps = True, []
        with open(OUT / f'blender-{bundle}.log', 'w', encoding='utf-8') as log:
            for item in PLAN[bundle]:
                ok, note = step(item, log)
                steps.append({'step': ' '.join(str(x) for x in item if not isinstance(x, tuple)) +
                              ''.join(' ' + ' '.join(x) for x in item if isinstance(x, tuple)),
                              'ok': ok, 'note': note})
                print(f'{bundle}: {"OK  " if ok else "FAIL"} {steps[-1]["step"]}: {note}')
                if not ok:
                    ok_all = False
                    break
        report[bundle] = {'ok': ok_all, 'steps': steps}
    (OUT / 'blender-report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return 0 if all(report[b]['ok'] for b in bundles) else 1


def hashes():
    files = {}
    for folder in sorted((WORKSHOP / 'assets').glob('*/out')):
        for file in sorted(folder.rglob('*')):
            if file.is_file() and file.suffix.lower() in ('.fbx', '.png', '.json'):
                files[file.relative_to(WORKSHOP).as_posix()] = {
                    'bytes': file.stat().st_size, 'sha256': hashlib.sha256(file.read_bytes()).hexdigest()}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'blender-outputs.json').write_text(json.dumps({'workshop_pin': WORKSHOP_PIN, 'blender': str(BLENDER.name),
                                                          'files': files}, indent=2) + '\n', encoding='utf-8')
    print(f'hashed {len(files)} Blender outputs into build/assets/blender-outputs.json')


def main(argv):
    if not argv or argv[0] not in ('stage', 'blender', 'hashes'):
        print(__doc__)
        return 2
    if argv[0] == 'stage':
        stage()
        return 0
    if argv[0] == 'hashes':
        hashes()
        return 0
    bundles = argv[1:] or list(PLAN)
    unknown = [b for b in bundles if b not in PLAN]
    if unknown:
        raise SystemExit(f'unknown bundle(s): {", ".join(unknown)}; known: {", ".join(PLAN)}')
    return run_blender(bundles)


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
