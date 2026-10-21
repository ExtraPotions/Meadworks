"""Sounds and lights for the headsman's Blender scene (blender_scene.py).

Sounds: the bake lists sound cues by frame (sequence.json soundFrames/soundCues); each cue is one or more layers of
sounds, picked in turn from the layer's variants and trimmed to the layer's longest length, as sound strips in the
video sequencer, so they play with the timeline (the scene syncs to audio). The sounds are our own, made by sfx.py
into out/sfx (the game's swings, impacts and rumbles are nearly all bass); only the re-forming axe's two are the game's
own, from the local reference export (never shipped), and the player's greataxe plays the game's Battleaxe sounds as
the game mixes them. Lights: a baked part named light_* gets a green point light on it
whose strength follows the part's size frame by frame (the glow at the grip while the new axe forms, the glow where a
skeleton rises).
"""
import os

import bpy

REFERENCE = os.path.join(os.path.expanduser('~'), 'ValheimReference', 'ExportedProject', 'Assets')
GLOW = (0.45, 1.0, 0.72)
WATTS_PER_METRE = 2500.0

SFX = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'out', 'sfx')
OWN = None     # the layer's variants are the cue's sounds mixed by sfx.py: out/sfx/build_<time>/<cue>_<n>.wav


# cue: [(variants, volume, longest seconds), ...] - every layer plays at once. Only the re-forming axe's two sounds are
# still the game's own (the user likes them); the rest are made by sfx.py, the game's being almost all bass.
SOUNDS = {
    'vocal': [(OWN, 0.45, 2.0)],
    'vocal_raise': [(OWN, 0.4, 2.0)],
    'creak': [(OWN, 0.4, 1.2)],
    'whoosh_heavy': [(OWN, 0.35, 1.0)],
    'whoosh_short': [(OWN, 0.35, 0.5)],
    'whoosh_spin': [(OWN, 0.35, 1.0)],
    'throw': [(OWN, 0.3, 1.0)],
    # The player's greataxe: the game's Battleaxe swing and hit, mixed by sfx.py (PLAYER) at the game's own balance.
    'g_swing': [(OWN, 1.0, 1.5)],
    'g_hit': [(OWN, 1.0, 1.0)],
    'impact_ground': [(OWN, 0.55, 1.0)],
    'impact_hit': [(OWN, 0.45, 1.0)],
    'grind': [(OWN, 0.4, 1.0)],
    'shatter': [(OWN, 0.5, 1.5)],
    'regen': [(['Characters/Dverger/sfx/Attacks/Enemy_Dverger_ChargeUp_Big_01.ogg'], 0.7, 2.0)],
    'solid': [(['Characters/Fader/sfx/CharredSummon/Enemy_Father_CharredSummon_Impact_0%d.ogg' % n for n in (1, 2)],
               0.5, 1.2)],
}


def sounds(cuts, fps):
    """Every cue as sound strips at its frame; returns how many strips were placed."""
    scene = bpy.context.scene
    editor = scene.sequence_editor_create()
    strips = editor.strips if hasattr(editor, 'strips') else editor.sequences
    ends, used, placed = {}, {}, 0
    for frame, cue in sorted(zip(cuts.get('soundFrames', []), cuts.get('soundCues', []))):
        for layer, (variants, volume, longest) in enumerate(SOUNDS.get(cue, [])):
            paths = _own(cue) if variants is OWN else [os.path.join(REFERENCE, v) for v in variants]
            n = used.get((cue, layer), 0)
            used[(cue, layer)] = n + 1
            path = paths[n % len(paths)] if paths else os.path.join(SFX, cue)
            if not os.path.exists(path):
                print('WORKSHOP missing sound', path)
                continue
            placed += _strip(strips, ends, path, frame + 1, volume, int(longest * fps))
    try:
        scene.sync_mode = 'AUDIO_SYNC'
    except TypeError:
        pass
    return placed


def _own(cue):
    """The cue's variants that sfx.py made last (its newest build folder), in order."""
    builds = sorted(d for d in os.listdir(SFX) if d.startswith('build_')) if os.path.isdir(SFX) else []
    if not builds:
        return []
    folder = os.path.join(SFX, builds[-1])
    names = [f for f in os.listdir(folder) if f.startswith(cue + '_') and f[len(cue) + 1:-4].isdigit()]
    return [os.path.join(folder, f) for f in sorted(names, key=lambda f: int(f[len(cue) + 1:-4]))]


def _strip(strips, ends, path, start, volume, longest):
    channel = next(c for c in range(1, 128) if ends.get(c, 0) < start)
    strip = strips.new_sound(os.path.basename(path), path, channel, start)
    strip.volume = volume
    if strip.frame_final_duration > longest:
        strip.frame_final_duration = longest
    ends[channel] = strip.frame_final_end
    return 1


def light(obj, scales, keyer):
    """A green point light on `obj`, its strength following the part's size (`scales`, one per frame from frame 1)."""
    data = bpy.data.lights.new('light_' + obj.name, 'POINT')
    data.color = GLOW
    data.shadow_soft_size = 0.15
    lamp = bpy.data.objects.new('lamp_' + obj.name, data)
    bpy.context.collection.objects.link(lamp)
    lamp.parent = obj
    values = []
    for f, size in enumerate(scales):
        values += [f + 1, WATTS_PER_METRE * size]
    keyer(data, {'energy': [values]})
