// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Bestiary.Runtime.Native.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// The Crypt Executioner's sound cues, played locally on each peer from the game's own clips as the preview mixed
    /// them (<see cref="HeadsmanSoundTable"/>): a variant at random, each of its layers a clip started where the recipe
    /// says, filtered and faded (<see cref="HeadsmanVoice"/>). The clips are found by name among the game's loaded sounds,
    /// each played through the game's own audio source settings for it (its mixer group, so the game's volume sliders
    /// apply, and its distances). Nothing of the game's audio is shipped. Silent on a dedicated server. The clips are
    /// looked for once a session (searching every loaded object is slow); a later world join looks again only when one
    /// it found was unloaded since.
    /// </summary>
    public static class HeadsmanSounds
    {
        private const string Voice = "Enemy_Skeleton_Basic_Verse_Attack_01";
        private static readonly Dictionary<string, (AudioClip clip, AudioSource? source)> clips = new Dictionary<string, (AudioClip, AudioSource?)>();
        private static AudioSource? fallback;
        private static bool scanned, rescanned;

        private static bool Headless => SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;

        /// <summary>Finds every clip the table names; logs the ones the game has not loaded (their layers stay silent).</summary>
        public static void Load(ZNetScene scene)
        {
            if (Headless || (scanned && !Forget()))
            {
                return;
            }
            (scanned, rescanned) = (true, false);
            Scan();
            string[] missing = Wanted().Where(name => !clips.ContainsKey(name)).ToArray();
            if (missing.Length > 0)
            {
                Log.Info($"Crypt Executioner: {missing.Length} sound clips not loaded yet ({string.Join(", ", missing)}); looked for again at the first cue.");
            }
        }

        /// <summary>Drops the found clips the game has unloaded since (or whose sound source it destroyed); true if any.</summary>
        private static bool Forget()
        {
            string[] gone = clips.Where(entry => entry.Value.clip == null || (entry.Value.source is object && entry.Value.source == null))
                .Select(entry => entry.Key).ToArray();
            foreach (string name in gone)
            {
                clips.Remove(name);
            }
            return gone.Length > 0;
        }

        private static IEnumerable<string> Wanted() =>
            HeadsmanSoundTable.Cues.Values.SelectMany(variants => variants).SelectMany(layers => layers).Select(layer => layer.Clip).Distinct();

        /// <summary>The clips of the game's sound effects with their sources, then any other loaded clip of the name.</summary>
        private static void Scan()
        {
            var wanted = new HashSet<string>(Wanted());
            foreach (ZSFX sfx in Resources.FindObjectsOfTypeAll<ZSFX>())
            {
                foreach (AudioClip clip in sfx.m_audioClips.Where(c => c != null && wanted.Contains(c.name) && !clips.ContainsKey(c.name)))
                {
                    clips[clip.name] = (clip, sfx.GetComponent<AudioSource>());
                }
            }
            foreach (AudioClip clip in Resources.FindObjectsOfTypeAll<AudioClip>().Where(c => wanted.Contains(c.name) && !clips.ContainsKey(c.name)))
            {
                clips[clip.name] = (clip, null);
            }
            fallback = clips.TryGetValue(Voice, out var voice) ? voice.source : fallback;
        }

        /// <summary>The cue at `at`, a variant at random.</summary>
        public static void Play(string cue, Vector3 at)
        {
            if (Headless || !HeadsmanSoundTable.Cues.TryGetValue(cue, out HeadsmanSoundLayer[][] variants) || variants.Length == 0)
            {
                return;
            }
            HeadsmanSoundLayer[] layers = variants[Random.Range(0, variants.Length)];
            if (!rescanned && layers.Any(layer => !clips.ContainsKey(layer.Clip)))
            {
                rescanned = true;
                Scan();
            }
            foreach (HeadsmanSoundLayer layer in layers)
            {
                if (clips.TryGetValue(layer.Clip, out var found))
                {
                    HeadsmanVoice.Play(found.clip, found.source ?? fallback, layer, at);
                }
            }
        }
    }
}
