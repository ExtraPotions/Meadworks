#!/usr/bin/env python3
# Bestiary: authored humanoid clips as editor curves. Copyright (C) 2026 expDARE (ExtraPotions Meadworks).
# SPDX-License-Identifier: GPL-3.0-or-later
"""Turns the authored clips exported from EliteCreaturesPack's GPL bundles (ecp_bundles.py export-clips) into editor
curves the Bestiary Unity project rebuilds the clips from, and decodes rebuilt bundles' clips for the content check.

  python tools/assets/clip_curves.py curves     build/assets/curves/<bundle>/<clip>.json for every bundle clip the
                                                Unity step rebuilds (crossbowman, headsman, slinger), Q3 re-key
                                                applied where the design says (see NEUTRAL)

A clip in a built bundle holds no editor curves, only Unity's compiled muscle clip: streamed Hermite segments, dense
samples and constants, one float curve per binding of m_ClipBindingConstant. For the humanoid clips every binding is
an Animator muscle binding (typeID 95, customType 8) whose attribute indexes Unity's humanoid value list: 0..6 motion,
7..13 root (RootT, RootQ), 14..41 the four IK goals (T, Q), 42..136 the 95 muscles (HumanTrait.MuscleName order),
then translation DoF. The output keeps the attribute index; the Unity side names the curve from HumanTrait.

The nine clips of design 10.1 rule 1 are never read: they are not in the export.
"""
from pathlib import Path
import json, math, struct, sys

ROOT = Path(__file__).resolve().parents[2]
CLIPS = ROOT / 'build/assets/clips'
CURVES = ROOT / 'build/assets/curves'
FORBIDDEN_CLIPS = {
    'ecp_xbow_idle', 'ecp_xbow_walk', 'ecp_xbow_run',
    'ecp_headsman_idle', 'ecp_headsman_walk', 'ecp_headsman_run',
    'ecp_atgeir_player_attack0', 'ecp_atgeir_player_attack1', 'ecp_atgeir_player_attack2',
}
# Bundle -> the clips the Bestiary Unity step rebuilds (the exported authored clips, never the nine).
REBUILT = {
    'ecp_crossbowman': ['ecp_xbow_aim', 'ecp_xbow_fire', 'ecp_xbow_player_reload', 'ecp_xbow_player_reload_done'],
    'ecp_headsman': ['ecp_headsman_slam', 'ecp_headsman_scrape', 'ecp_headsman_spin', 'ecp_headsman_hurl',
                     'ecp_headsman_spinthrow', 'ecp_headsman_rear', 'ecp_headsman_stagger'],
    'ecr_slinger': ['ecr_sling_shot'],
}
# Q3 (user decision 2026-10-07): re-key a neutral pose in the bundles being rebuilt. These clips start and end on one
# copied pose, the game idle's first frame; the slinger's shot ships as authored.
NEUTRAL = {'ecp_xbow_aim', 'ecp_xbow_fire', 'ecp_headsman_slam', 'ecp_headsman_scrape', 'ecp_headsman_spin',
           'ecp_headsman_hurl', 'ecp_headsman_spinthrow', 'ecp_headsman_rear'}
MUSCLES = range(42, 137)
NEG_INF, POS_INF = -3.4028234663852886e38, 3.4028234663852886e38


def as_float(word):
    return struct.unpack('<f', struct.pack('<I', word & 0xFFFFFFFF))[0]


def streamed_keys(words, count):
    """Per curve, its keys as (time, a, b, c, d): the segment from a key to the next is a t^3 + b t^2 + c t + d."""
    keys = [[] for _ in range(count)]
    pos = 0
    while pos < len(words):
        time = as_float(words[pos])
        n = words[pos + 1]
        pos += 2
        for _ in range(n):
            index = words[pos]
            a, b, c, d = (as_float(w) for w in words[pos + 1:pos + 5])
            keys[index].append((time, a, b, c, d))
            pos += 5
    return keys


def curves_of(tree):
    """Every float curve of a compiled clip as {'attribute', 'keys': [[time, value, inSlope, outSlope], ...]}."""
    clip = tree['m_MuscleClip']
    data = clip['m_Clip']['data']
    start, stop = clip['m_StartTime'], clip['m_StopTime']
    bindings = tree['m_ClipBindingConstant']['genericBindings']
    streamed = data['m_StreamedClip']
    dense, constant = data['m_DenseClip'], data['m_ConstantClip']['data']
    if any(b['typeID'] != 95 or b['customType'] != 8 for b in bindings):
        raise ValueError(f'{tree["m_Name"]}: only humanoid muscle bindings are handled')
    out = []
    raw = streamed_keys(streamed['data'], streamed['curveCount'])
    for index, segment in enumerate(raw):
        keys = []
        real = [k for k in segment if NEG_INF < k[0] < POS_INF]
        for i, (t, a, b, c, d) in enumerate(real):
            if i == 0:
                in_slope = c
            else:
                pt, pa, pb, pc, _ = real[i - 1]
                dt = t - pt
                in_slope = 3 * pa * dt * dt + 2 * pb * dt + pc
            keys.append([t, d, in_slope, c])
        if keys:
            keys[-1][3] = keys[-1][2]
        out.append({'attribute': bindings[index]['attribute'], 'keys': keys})
    count, frames = dense['m_CurveCount'], dense['m_FrameCount']
    samples, rate, begin = dense['m_SampleArray'], dense['m_SampleRate'], dense['m_BeginTime']
    for i in range(count):
        values = [samples[f * count + i] for f in range(frames)]
        keys = []
        for f, v in enumerate(values):
            prev = (v - values[f - 1]) * rate if f > 0 else 0.0
            nxt = (values[f + 1] - v) * rate if f + 1 < frames else prev
            keys.append([begin + f / rate, v, prev if f > 0 else nxt, nxt])
        out.append({'attribute': bindings[streamed['curveCount'] + i]['attribute'], 'keys': keys, 'dense': True})
    for offset, value in enumerate(constant):
        out.append({'attribute': bindings[streamed['curveCount'] + count + offset]['attribute'],
                    'keys': [[start, value, 0.0, 0.0], [stop, value, 0.0, 0.0]]})
    return out


def evaluate(keys, t):
    """Hermite evaluation of Unity keys at t (clamped outside)."""
    if t <= keys[0][0]:
        return keys[0][1]
    for k0, k1 in zip(keys, keys[1:]):
        if t <= k1[0]:
            dt = k1[0] - k0[0]
            if dt <= 0:
                return k1[1]
            s = (t - k0[0]) / dt
            h00, h10 = 2 * s ** 3 - 3 * s ** 2 + 1, s ** 3 - 2 * s ** 2 + s
            h01, h11 = -2 * s ** 3 + 3 * s ** 2, s ** 3 - s ** 2
            return h00 * k0[1] + h10 * dt * k0[3] + h01 * k1[1] + h11 * dt * k1[2]
    return keys[-1][1]


def neutral(curves, start, stop):
    """Q3 re-key. The copied pose is what every curve holds at the clip's first instant (the game idle's first frame);
    a clip that comes back to it holds it again at its last instant. A muscle key on that pose becomes Unity's own
    neutral muscle value (0), and a muscle the clip never moves (it held the copied value throughout) is 0 everywhere.
    Root and goal curves (body placement, not a joint pose) take the clip's nearest own key inside the clip at those
    instants. Every authored key in between is kept. Returns a change summary."""
    changed = {'start_keys': 0, 'end_keys': 0, 'flat_muscles': 0}
    for curve in curves:
        keys = curve['keys']
        copied = keys[0][1]
        flat = all(abs(k[1] - copied) < 1e-6 for k in keys)
        first, last = keys[0], keys[-1]
        at_start = abs(first[0] - start) < 1e-4
        at_end = abs(last[0] - stop) < 1e-4 and abs(last[1] - copied) < 1e-4 and len(keys) > 2
        if curve['attribute'] in MUSCLES:
            if flat:
                for k in keys:
                    k[1], k[2], k[3] = 0.0, 0.0, 0.0
                changed['flat_muscles'] += 1
                continue
            if at_start:
                first[1], first[2], first[3] = 0.0, 0.0, 0.0
                changed['start_keys'] += 1
            if at_end:
                last[1], last[2], last[3] = 0.0, 0.0, 0.0
                changed['end_keys'] += 1
        elif not flat and len(keys) > 2:
            if at_start:
                first[1], first[2], first[3] = keys[1][1], 0.0, 0.0
                changed['start_keys'] += 1
            if at_end:
                last[1], last[2], last[3] = keys[-2][1], 0.0, 0.0
                changed['end_keys'] += 1
    return changed


def settings_of(tree):
    clip = tree['m_MuscleClip']
    names = ['m_Mirror', 'm_LoopTime', 'm_LoopBlend', 'm_LoopBlendOrientation', 'm_LoopBlendPositionY',
             'm_LoopBlendPositionXZ', 'm_KeepOriginalOrientation', 'm_KeepOriginalPositionY',
             'm_KeepOriginalPositionXZ', 'm_HeightFromFeet', 'm_StartTime', 'm_StopTime', 'm_OrientationOffsetY',
             'm_Level', 'm_CycleOffset']
    return {n[2].lower() + n[3:]: clip[n] for n in names}


def convert(bundle, name):
    if name in FORBIDDEN_CLIPS:
        raise SystemExit(f'{name} is one of the nine excluded clips')
    source = CLIPS / bundle / 'windows' / f'{name}.json'
    tree = json.loads(source.read_text(encoding='utf-8'))
    curves = curves_of(tree)
    settings = settings_of(tree)
    record = {'name': name, 'source': source.relative_to(ROOT).as_posix(), 'sampleRate': tree['m_SampleRate'],
              'settings': settings,
              'events': [{'time': e['time'], 'functionName': e['functionName'], 'data': e['data'],
                          'floatParameter': e['floatParameter'], 'intParameter': e['intParameter']}
                         for e in tree['m_Events']],
              'neutral': None, 'curves': curves}
    if name in NEUTRAL:
        record['neutral'] = neutral(curves, settings['startTime'], settings['stopTime'])
    # Unity's JsonUtility reads no nested arrays: each curve's keys go out flat (time, value, inSlope, outSlope)...
    written = dict(record, curves=[{'attribute': c['attribute'], 'keys': [x for k in c['keys'] for x in k]}
                                   for c in curves])
    target = CURVES / bundle / f'{name}.json'
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(written) + '\n', encoding='utf-8')
    return record


def main(argv):
    if argv[:1] != ['curves']:
        print(__doc__)
        return 2
    for bundle, names in REBUILT.items():
        for name in names:
            r = convert(bundle, name)
            keys = sum(len(c['keys']) for c in r['curves'])
            print(f'{bundle}/{name}: {len(r["curves"])} curves, {keys} keys, length {r["settings"]["stopTime"]:.3f}s, '
                  f'{len(r["events"])} events, neutral {r["neutral"]}')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
