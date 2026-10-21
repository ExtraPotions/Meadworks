// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The faint ringing under a shriek's hush: a soft high tone of two pitches four hertz apart, so it wavers the way a
    /// ringing ear does, made once in code (one second of samples holding whole cycles of both, so the loop is
    /// seamless) and played flat, from no place in the world. It rings through the hush (the listener volume is not
    /// applied to it) yet goes out through the game's interface channel, so the player's own volume settings still
    /// govern it; with no such channel it stays silent rather than play past those settings. It swells as the world
    /// falls quiet and fades as hearing returns.
    /// </summary>
    internal sealed class ShriekRing
    {
        private const int Rate = 44100;
        private const int Low = 3000;
        private const int High = 3004;

        /// <summary>Its loudness at the depth of the hush: faint.</summary>
        private const float Peak = 0.05f;

        private static AudioClip? _clip;

        /// <summary>The next sample the engine will ask for while the clip is being filled.</summary>
        private static int _cursor;

        private readonly AudioSource _source;

        private ShriekRing(AudioSource source) => _source = source;

        /// <summary>A ringing under <paramref name="parent"/>, silent until <see cref="Follow"/>; null with nowhere to play it.</summary>
        public static ShriekRing? Begin(Transform parent)
        {
            AudioMan audio = AudioMan.instance;
            if (audio == null || audio.m_guiMixer == null)
            {
                return null;
            }
            GameObject holder = new GameObject("ecr_ringing");
            holder.transform.SetParent(parent, worldPositionStays: false);
            AudioSource source = holder.AddComponent<AudioSource>();
            source.clip = Clip();
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerVolume = true;
            source.outputAudioMixerGroup = audio.m_guiMixer;
            source.Play();
            return new ShriekRing(source);
        }

        /// <summary>Louder the deeper the hush: <paramref name="level"/> is how much of the world's sound passes.</summary>
        public void Follow(float level)
        {
            if (_source != null)
            {
                _source.volume = Peak * (1f - level);
            }
        }

        public void End()
        {
            if (_source != null)
            {
                Object.Destroy(_source.gameObject);
            }
        }

        // Filled through the clip's own reader at creation rather than SetData, whose span overload the mod's .NET
        // Framework build cannot compile against; the engine asks for the samples in order, from the position it gives.
        private static AudioClip Clip()
        {
            if (_clip != null)
            {
                return _clip;
            }
            _cursor = 0;
            AudioClip clip = AudioClip.Create("ecr_ringing", Rate, 1, Rate, false, Fill, Seek);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _clip = clip;
            return clip;
        }

        private static void Seek(int position) => _cursor = position;

        private static void Fill(float[] data)
        {
            for (int i = 0; i < data.Length; i++, _cursor++)
            {
                int sample = _cursor % Rate;
                data[i] = 0.5f * (Wave(Low, sample) + Wave(High, sample));
            }
        }

        // Whole cycles per second, the phase worked in integers so it never drifts across the second.
        private static float Wave(int hertz, int sample) => Mathf.Sin(2f * Mathf.PI * (hertz * sample % Rate) / Rate);
    }
}
