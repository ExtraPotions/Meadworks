"""The mod's table of the headsman's sounds (EliteCreaturesPack Headsman/Sound/HeadsmanSoundTable.cs), from the same
recipes the Blender preview plays (sfx.py, blender_fx.SOUNDS), so the game sounds as the preview did.

For every cue, variant and layer: the game clip by name, where in it to start (a layer whose peak must land sooner
than it comes is cut at the start, and fades in over 10 ms), how long after the cue it starts, its volume, its high-
and low-pass, its pitch, and how long it plays (the layer's own longest, within the preview strip's). The volume folds
together what the preview did to the mix: the clip brought to peak 1, the layer's gain, the mix brought to a common
loudness (sfx.loudness), the strip's volume; then one factor for all, so the boss's voice is as loud as the game's own
skeleton voice (its sfx plays it at 0.8). Only the re-forming's two cues are the game's clips as they are. The player's
greataxe has no row: it plays the game's own Battleaxe swing and hit, through the game's sound prefabs.

Run in Blender (its sound library decodes the game's .ogg files; build.ps1 does, after sfx.py):
    blender --background --factory-startup --python sfx_table.py -- <table.cs>
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import blender_fx  # noqa: E402
import sfx  # noqa: E402

VOICE = 0.8          # the game's sfx_skeleton_basic_verse_attack plays the skeleton's voice at 0.8
FADE_IN = 0.01
TABLE = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', '..', 'EliteCreaturesPack',
                     'EliteCreaturesPack', 'Headsman', 'Sound', 'HeadsmanSoundTable.cs')


def peak(rel):
    """The clip's loudest sample as the game has it (the mono mix sfx.game brings to 1)."""
    import aud
    data = np.asarray(aud.Sound(os.path.join(sfx.REFERENCE, rel)).data(), dtype=np.float64)
    x = data.mean(axis=1) if data.ndim > 1 else data
    return float(np.abs(x).max())


def place(layer, n):
    """(file, start in the clip's own seconds, delay after the cue, cut): where sfx.clip puts variant n."""
    rel = layer.files % layer.numbers[n % len(layer.numbers)] if '%d' in layer.files else layer.files
    if layer.peak is None:
        return rel, 0.0, layer.delay, False
    pitch = layer.pitch[n % len(layer.pitch)]
    x = sfx.game(rel)
    if pitch != 1.0:
        x = np.interp(np.arange(0, len(x) - 1, pitch), np.arange(len(x)), x)
    x = sfx.band(x, layer.low, layer.high)
    top = int(np.argmax(np.abs(x)))
    cut = max(0, top - sfx.samples(layer.peak))
    if cut:
        return rel, cut / sfx.SR * pitch, 0.0, True
    kept = x[:sfx.samples(layer.longest)]
    return rel, 0.0, max(0.0, layer.peak - int(np.argmax(np.abs(kept))) / sfx.SR), False


def own(cue, layers, strip_volume, strip_longest):
    """The cue's variants from sfx.RECIPES: per variant, per layer, the table's row before the common factor."""
    variants = max(max(len(layer.numbers), len(layer.pitch)) for layer in layers)
    rows = []
    for n in range(variants):
        loud = sfx.loudness(sfx.trim(sfx.mix(layers, n)))
        variant = []
        for layer in layers:
            rel, start, delay, cut = place(layer, n)
            variant.append(dict(clip=os.path.splitext(os.path.basename(rel))[0], start=start, delay=delay,
                                volume=layer.gain * loud * strip_volume / peak(rel), low=layer.low, high=layer.high,
                                pitch=layer.pitch[n % len(layer.pitch)],
                                longest=min(layer.longest, strip_longest - delay), fade=cut))
        rows.append(variant)
    return rows


def game_cue(files, strip_volume, strip_longest):
    """A cue of the game's clips as they are (the re-forming's two): one variant per file."""
    return [[dict(clip=os.path.splitext(os.path.basename(f))[0], start=0.0, delay=0.0, volume=strip_volume, low=0.0,
                  high=0.0, pitch=1.0, longest=strip_longest, fade=False)] for f in files]


def cues():
    """Every cue of the preview but the player's greataxe (sfx.PLAYER: the game's Battleaxe sounds, which the mod leaves
    on the item and the game plays itself)."""
    table = {}
    for cue, strips in blender_fx.SOUNDS.items():
        if cue in sfx.PLAYER:
            continue
        variants, volume, longest = strips[0]
        table[cue] = own(cue, sfx.RECIPES[cue], volume, longest) if variants is blender_fx.OWN \
            else game_cue(variants, volume, longest)
    voice = np.mean([layer['volume'] for variant in table['vocal'] for layer in variant])
    factor = VOICE / voice
    for variant in (v for rows in table.values() for v in rows):
        for layer in variant:
            layer['volume'] = min(1.0, layer['volume'] * factor)
    return table, factor


def number(value):
    return ('%.3f' % value).rstrip('0').rstrip('.') + 'f' if value else '0f'


def row(layer):
    return 'L("%s", %s, %s, %s, %s, %s, %s, %s, %s)' % (
        layer['clip'], number(layer['start']), number(layer['delay']), number(layer['volume']), number(layer['low']),
        number(layer['high']), number(layer['pitch']), number(layer['longest']), 'true' if layer['fade'] else 'false')


def write(path, table, factor):
    lines = ['// Written by AssetWorkshop/assets/ecp_headsman/sfx_table.py from the preview\'s sound recipes (sfx.py,',
             '// blender_fx.SOUNDS); change those and run build.ps1, not this file.',
             'using System.Collections.Generic;', '', 'namespace EliteCreaturesPack.Headsman', '{',
             '    /// <summary>',
             '    /// The Crypt Executioner\'s sound cues as the preview mixed them: each cue\'s variants, each the layers of game',
             '    /// clips played together (<see cref="HeadsmanSoundLayer"/>). One factor (%.2f) brings them all to the level' % factor,
             '    /// the game plays its own skeleton\'s voice at.',
             '    /// </summary>',
             '    public static class HeadsmanSoundTable', '    {',
             '        public static readonly Dictionary<string, HeadsmanSoundLayer[][]> Cues = new Dictionary<string, HeadsmanSoundLayer[][]>',
             '        {']
    for cue, variants in table.items():
        lines.append('            ["%s"] = new[]' % cue)
        lines.append('            {')
        for variant in variants:
            lines.append('                new[] { %s },' % ', '.join(row(layer) for layer in variant))
        lines.append('            },')
    lines += ['        };', '',
              '        private static HeadsmanSoundLayer L(string clip, float start, float delay, float volume, float low, float high, float pitch, float longest, bool fadeIn) =>',
              '            new HeadsmanSoundLayer(clip, start, delay, volume, low, high, pitch, longest, fadeIn);',
              '    }', '}', '']
    with open(path, 'w', encoding='utf-8', newline='\r\n') as f:
        f.write('\n'.join(lines))


def main(path):
    table, factor = cues()
    write(path, table, factor)
    loudest = max(layer['volume'] for rows in table.values() for v in rows for layer in v)
    print('WORKSHOP sfx table: %d cues, factor %.2f, loudest layer %.2f -> %s' % (len(table), factor, loudest,
                                                                                   os.path.normpath(path)))


if __name__ == '__main__':
    main(sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv and len(sys.argv) > sys.argv.index('--') + 1 else TABLE)
