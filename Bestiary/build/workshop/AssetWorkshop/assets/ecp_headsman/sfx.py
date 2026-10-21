"""The headsman's sounds: the game's own recordings, chosen for a skeleton with a bone axe and cleaned of sub-bass.

Two lessons. The game's big swing, impact and stomp sounds are made for trolls and sledges, most of their energy below
80 Hz (the player's axe swings 89-93 %, the troll's impacts about 80 %): far too much bass for this fight. And sounds
synthesized from scratch (noise sweeps, tuned knocks) came out like cartoon noises that do not fit Valheim at all. So
every cue here is real Valheim recordings, picked for what happens and its size (the Skeleton's own melee swing, the
game's axe hit, the hoe biting into dirt, the cultivator dragging through soil, Fader's stone pillars pushing up out
of the ground, the Skeleton's bone shatters), layered, started where their peak lands on the moment, cut before any
metal ring (a ting does not fit), and high-passed just enough to take the sub-bass off while keeping their body.

Only what the mod can do at runtime with the game's own clips: a volume, a delay or a start time into the clip, a
pitch (AudioSource.pitch) and high- and low-pass filters (AudioHighPassFilter, AudioLowPassFilter). The files
written here are a preview mix; the mod plays the same recipes with the game's clips and never ships them.

Run in Blender, whose sound library decodes the game's .ogg files (build.ps1 does):
    blender --background --factory-startup --python sfx.py -- <out dir>
Writes <out dir>/build_<time>/<cue>_<n>.wav, which blender_fx.py places on the timeline, and reports each with its
energy per band.
"""
import collections
import os
import shutil
import sys
import time
import wave

import numpy as np

SR = 48000
REFERENCE = os.path.join(os.path.expanduser('~'), 'ValheimReference', 'ExportedProject', 'Assets')
BANDS = (0, 80, 160, 300, 600, 1500, 4000, SR // 2)
KEEP = 3                       # older build folders kept
LOUDNESS, PEAK = 0.12, 0.9     # the loudest 0.1 s is brought to this RMS, unless the peak would pass PEAK
_WAV = 'Characters/Skeleton/fx/wav/'
_NEW = 'Characters/Skeleton/fx/New sfx/'

# One layer of a cue: `files` (a %d pattern and its numbers: variant n takes the n-th, round and round), `peak`
# (seconds after the cue the clip's loudest moment lands: its start is cut to fit) or else `delay` (seconds after the
# cue it starts), `gain`, `low` (high-pass Hz), `high` (low-pass Hz, 0 for none), `pitch` (one per variant, round
# and round), `longest` (seconds kept).
Layer = collections.namedtuple('Layer', 'files numbers peak delay gain low high pitch longest',
                               defaults=(None, 0.0, 1.0, 100.0, 0.0, (1.0,), 2.0))

RECIPES = {
    # The Skeleton's own melee swing, its peak where the axe is fastest (the cues sit 0.13-0.24 s before the blow),
    # cut before its sword rings (a metal ting from 0.33 s in some takes).
    'whoosh_heavy': [Layer(_NEW + 'Basic/Enemy_Skeleton_Basic_Attack_Melee_0%d.ogg', (1, 2, 3), peak=0.17, low=90,
                           longest=0.32)],
    # The Deep North skeleton's swing, a little brighter, for the flat sweep round; cut before its ring (from 0.53 s).
    'whoosh_spin': [Layer(_NEW + 'DeepNorth/Enemy_Skeleton_DeepNorth_Attack_Melee_0%d.ogg', (1, 2, 3), peak=0.36,
                          low=110, longest=0.5)],
    # The player's spear throw: a weapon leaving the hand.
    'throw': [Layer('Characters/Player/audio/wav/weapons/spear/Player_Movement_SpearThrow_M_0%d.ogg', (1, 2, 3),
                    peak=0.06, low=120)],
    # The slam's swing, short: it stops as the blow lands (the cue sits 0.24 s before it).
    'whoosh_short': [Layer(_NEW + 'Basic/Enemy_Skeleton_Basic_Attack_Melee_0%d.ogg', (1, 2, 3), peak=0.17, low=90,
                           longest=0.3)],
    # The slam landing, no rubble (the user): the axe hitting (the game's own axe hit) and biting into the dirt (the
    # hoe digging).
    'impact_ground': [Layer('GameElements/Items/weapons/_res/battleaxe/sfx/Player_Movement_Axe_Hit_M_0%d.ogg', (1, 3, 5),
                            peak=0.0, low=130),
                      Layer('Audio/sfx/inventory_gui/wav/build/UI_Hoe_0%d.ogg', (4, 5, 1), peak=0.01, gain=0.7, low=120,
                            longest=0.35)],
    # The rear strike landing: the player's battleaxe hitting.
    'impact_hit': [Layer('GameElements/Items/weapons/_res/battleaxe/sfx/Player_Movement_Axe_Hit_M_0%d.ogg', (2, 4, 5),
                         peak=0.0, low=150)],
    # The edge dragged through the ground: the hoe biting in, the cultivator's scrape through soil, slowed to the drag.
    'grind': [Layer('Audio/sfx/inventory_gui/wav/build/UI_Hoe_0%d.ogg', (1, 3, 2), peak=0.0, gain=0.5, low=150),
              Layer('Audio/sfx/inventory_gui/wav/build/UI_Cultivator_0%d.ogg', (1, 2, 3), delay=0.03, low=150,
                    high=4500, pitch=(0.85,), longest=0.5)],
    # The thrown axe breaking: the Skeleton's bone shatter, then bones clattering down and settling.
    'shatter': [Layer(_WAV + 'Skeleton_Hit_Shatter%d.ogg', (1, 2, 1), peak=0.0, low=150),
                Layer(_WAV + 'Skeleton_Death_BoneHit%d.ogg', (1, 2, 3), delay=0.12, gain=0.55, low=150),
                Layer(_WAV + 'Skeleton_BonesRattle.ogg', (0,), delay=0.22, gain=0.4, low=150, longest=0.7)],
    # The voices, as they are, their low end off.
    'vocal': [Layer(_NEW + 'Basic/Enemy_Skeleton_Basic_Verse_Attack_0%d.ogg', (1, 3, 5, 7, 2, 4, 6), low=120)],
    'vocal_raise': [Layer(_NEW + 'Hildir/Enemy_Skeleton_Hildir_Attack_Skill_0%d.ogg', (1, 2), low=220)],
    'creak': [Layer(_NEW + 'Basic/Enemy_Skeleton_Basic_Verse_Idle_0%d.ogg', (2, 5), low=150)],
}

# The Executioner's Greataxe in a player's hands (the player preview, Workshop.Greataxe): the game's own Battleaxe
# sounds, which the mod leaves on the item, mixed as the game plays them - their clips as they are (no filter, no
# normalising), their sound prefab's volume, and pitches spread over its range, one variant after another as the game
# picks at random. The swing (sfx_battleaxe_swing_wosh: pitch 1.0-1.2, volume 0.8-0.9) plays at every combo step's
# trail, as the game plays it for all three Battleaxe swings and for the wooden greatsword's whirl; the hit
# (sfx_battleaxe_hit: pitch 0.9-1.0, volume 0.4-0.5) at each hit. Both play at full volume within 5 m, so these are
# their levels at the camera. An earlier set high-passed the swing at 240 Hz and borrowed a skeleton's sword swing and
# the sledge's for the spin and overhead: thin hiss with its body gone (centroid 0.7-2 kHz, where every game swing
# sits at 70-200 Hz), and the user said they did not belong in Valheim.
_AXE = 'GameElements/Items/weapons/_res/battleaxe/sfx/'
PLAYER = {
    'g_swing': [Layer(_AXE + 'Player_Movement_BattleAxe_Swing_0%d.ogg', (1, 2, 3, 4), gain=0.85, low=0.0,
                      pitch=(1.04, 1.17, 1.0, 1.1, 1.2, 1.07))],
    'g_hit': [Layer(_AXE + 'Player_Movement_Axe_Hit_M_0%d.ogg', (1, 2, 3, 4, 5), gain=0.45, low=0.0,
                    pitch=(0.95, 0.9, 0.98, 0.93, 1.0))],
}


def samples(seconds):
    return int(round(seconds * SR))


def band(x, low, high=0.0, order=2):
    """Zero-phase Butterworth-shaped high-pass at `low` Hz (and low-pass at `high`), by FFT: the magnitude of the mod's
    2nd-order AudioHighPassFilter and AudioLowPassFilter."""
    size = 1 << (2 * len(x) - 1).bit_length()
    f = np.fft.rfftfreq(size, 1.0 / SR)
    gain = 1.0 / np.sqrt(1.0 + (low / np.maximum(f, 1e-3)) ** (2 * order))
    if high:
        gain /= np.sqrt(1.0 + (f / high) ** (2 * order))
    return np.fft.irfft(np.fft.rfft(x, size) * gain, size)[:len(x)]


def game(rel, raw=False):
    """A sound from the local reference export, mono at SR, peak 1 (`raw`: at its own level)."""
    import aud
    sound = aud.Sound(os.path.join(REFERENCE, rel))
    rate = sound.specs[0]
    data = np.asarray(sound.data(), dtype=np.float64)
    x = data.mean(axis=1) if data.ndim > 1 else data
    if rate != SR:
        x = np.interp(np.arange(int(len(x) * SR / rate)) * rate / SR, np.arange(len(x)), x)
    return x if raw else x / (np.abs(x).max() + 1e-9)


def clip(layer, n, raw=False):
    """Variant `n` of a layer: the clip pitched (played faster or slower, as AudioSource.pitch does), high-passed,
    and where it starts after the cue, its start cut when its peak must land sooner than it comes."""
    rel = layer.files % layer.numbers[n % len(layer.numbers)] if '%d' in layer.files else layer.files
    x = game(rel, raw)
    pitch = layer.pitch[n % len(layer.pitch)]
    if pitch != 1.0:
        x = np.interp(np.arange(0, len(x) - 1, pitch), np.arange(len(x)), x)
    x = band(x, layer.low, layer.high)
    if layer.peak is None:
        return x[:samples(layer.longest)], layer.delay
    cut = max(0, int(np.argmax(np.abs(x))) - samples(layer.peak))
    x = x[cut:cut + samples(layer.longest)]
    x[:samples(0.01)] *= np.linspace(0.0, 1.0, samples(0.01))
    return x, max(0.0, layer.peak - np.argmax(np.abs(x)) / SR) if cut == 0 else 0.0


def mix(layers, n, raw=False):
    parts = [(clip(layer, n, raw), layer.gain) for layer in layers]
    out = np.zeros(max(samples(at) + len(x) for (x, at), _ in parts))
    for (x, at), gain in parts:
        out[samples(at):samples(at) + len(x)] += gain * x
    return out


def finish(x):
    """Faded out, the silent tail cut, brought to a common loudness."""
    x = trim(x)
    return x * loudness(x)


def trim(x):
    """The silent tail cut, the end faded out."""
    quiet = np.nonzero(np.abs(x) > np.abs(x).max() * 0.003)[0]
    x = x[:quiet[-1] + samples(0.02)] if len(quiet) else x
    x[-samples(0.05):] *= np.linspace(1.0, 0.0, samples(0.05))
    return x


def loudness(x):
    """The gain that brings the loudest 0.1 s to LOUDNESS, unless the peak would pass PEAK (the mod's table,
    sfx_table.py, folds it into each layer's volume)."""
    window = min(len(x), samples(0.1))
    rms = np.sqrt(np.convolve(x ** 2, np.ones(window) / window, 'valid').max())
    return min(LOUDNESS / (rms + 1e-9), PEAK / (np.abs(x).max() + 1e-9))


def write(path, x):
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((np.clip(x, -1.0, 1.0) * 32767).astype('<i2').tobytes())


def report(name, x):
    spec = np.abs(np.fft.rfft(x)) ** 2
    f = np.fft.rfftfreq(len(x), 1.0 / SR)
    total = spec.sum() + 1e-12
    shares = [spec[(f >= a) & (f < b)].sum() / total * 100 for a, b in zip(BANDS, BANDS[1:])]
    print('WORKSHOP sfx %-16s %.2f s, below 80 Hz %4.1f %%, centroid %5.0f Hz | bands %s' % (
        name, len(x) / SR, shares[0], (spec * f).sum() / total, ' '.join('%4.1f' % s for s in shares)))


def main(base):
    """Writes a new folder under `base` each time (an open preview keeps its files locked) and removes what it can of
    the older ones but the last KEEP (the boss's and the player's previews are rebuilt apart; each blend names the folder
    that was newest when it was built)."""
    out = os.path.join(base, time.strftime('build_%Y%m%d_%H%M%S'))
    os.makedirs(out)
    builds = sorted(d for d in os.listdir(base) if d.startswith('build_') and os.path.join(base, d) != out)
    for old in builds[:-KEEP] + [f for f in os.listdir(base) if not f.startswith('build_')]:
        shutil.rmtree(os.path.join(base, old), ignore_errors=True) if os.path.isdir(os.path.join(base, old)) else             _remove(os.path.join(base, old))
    for cue, layers in RECIPES.items():
        for n in range(variants(layers)):
            _save(out, cue, n, finish(mix(layers, n)))
    player = {cue: [trim(mix(layers, n, raw=True)) for n in range(variants(layers))] for cue, layers in PLAYER.items()}
    gain = played(player)
    for cue, sounds in player.items():
        for n, x in enumerate(sounds):
            _save(out, cue, n, x * gain)
    print('WORKSHOP sfx: %d files in %s (the player\'s at %.2f)' % (len(os.listdir(out)), out, gain))


def variants(layers):
    return max(max(len(layer.numbers), len(layer.pitch)) for layer in layers)


def played(player):
    """One gain for all the player's sounds, so they keep the game's balance (the swing as far under the hit as the
    game plays it): the hits' loudest 0.1 s, on average, at LOUDNESS, unless a peak would pass PEAK."""
    hits = [loudness(x) for x in player['g_hit']]
    top = max(np.abs(x).max() for sounds in player.values() for x in sounds)
    return min(float(np.median(hits)), PEAK / (top + 1e-9))


def _save(out, cue, n, x):
    write(os.path.join(out, '%s_%d.wav' % (cue, n + 1)), x)
    report('%s_%d' % (cue, n + 1), x)


def _remove(path):
    try:
        os.remove(path)
    except OSError:
        pass


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(os.path.dirname(__file__), 'out', 'sfx'))
