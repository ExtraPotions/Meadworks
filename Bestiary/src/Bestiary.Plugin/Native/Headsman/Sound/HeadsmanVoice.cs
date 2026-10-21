// Portions Copyright (C) 2026 Gerald Glasgow. EliteCreaturesPack 0.7.2 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Changed by expDARE, 2026: Bestiary namespace, owned Crucible settings, rebuilt-only bundles and lifecycle adapters.
// SPDX-License-Identifier: GPL-3.0-or-later
using UnityEngine;

namespace ExtraPotions.Bestiary.Runtime.Native.Headsman
{
    /// <summary>
    /// One layer of a sound cue playing (<see cref="HeadsmanSounds"/>): a local audio source with the game's settings for
    /// the clip, its high- and low-pass, started its delay after the cue at its place in the clip, faded in when cut at
    /// the start, faded out over its last 50 ms, and gone when done.
    /// </summary>
    public sealed class HeadsmanVoice : MonoBehaviour
    {
        private const float FadeIn = 0.01f, FadeOut = 0.05f;

        private AudioSource source = null!;
        private HeadsmanSoundLayer layer = null!;
        private float begins, length;
        private bool started;

        public static void Play(AudioClip clip, AudioSource? template, HeadsmanSoundLayer layer, Vector3 at)
        {
            var sound = new GameObject("ecp_headsman_sound");
            sound.transform.position = at;
            AudioSource source = sound.AddComponent<AudioSource>();
            Copy(template, source);
            (source.clip, source.pitch, source.volume, source.playOnAwake, source.loop) = (clip, layer.Pitch, 0f, false, false);
            Filter(sound, layer);
            var voice = sound.AddComponent<HeadsmanVoice>();
            (voice.source, voice.layer, voice.begins) = (source, layer, Time.time + layer.Delay);
            voice.length = Mathf.Min(layer.Longest, (clip.length - layer.Start) / Mathf.Max(0.1f, layer.Pitch));
        }

        /// <summary>The game's source settings for the clip (its mixer group, distances, spread), or plain 3D ones.</summary>
        private static void Copy(AudioSource? from, AudioSource to)
        {
            (to.spatialBlend, to.rolloffMode, to.minDistance, to.maxDistance) = (1f, AudioRolloffMode.Linear, 5f, 25f);
            if (from == null)
            {
                return;
            }
            to.outputAudioMixerGroup = from.outputAudioMixerGroup;
            (to.spatialBlend, to.rolloffMode, to.minDistance, to.maxDistance) = (from.spatialBlend, from.rolloffMode, from.minDistance, from.maxDistance);
            (to.dopplerLevel, to.spread, to.priority, to.reverbZoneMix) = (from.dopplerLevel, from.spread, from.priority, from.reverbZoneMix);
            if (from.rolloffMode == AudioRolloffMode.Custom)
            {
                to.SetCustomCurve(AudioSourceCurveType.CustomRolloff, from.GetCustomCurve(AudioSourceCurveType.CustomRolloff));
            }
        }

        private static void Filter(GameObject sound, HeadsmanSoundLayer layer)
        {
            if (layer.Low > 0f)
            {
                sound.AddComponent<AudioHighPassFilter>().cutoffFrequency = layer.Low;
            }
            if (layer.High > 0f)
            {
                sound.AddComponent<AudioLowPassFilter>().cutoffFrequency = layer.High;
            }
        }

        private void Update()
        {
            float t = Time.time - begins;
            if (t < 0f)
            {
                return;
            }
            if (!started)
            {
                source.time = Mathf.Clamp(layer.Start, 0f, source.clip.length - 0.01f);
                source.Play();
                started = true;
            }
            if (t >= length || !source.isPlaying && t > 0.1f)
            {
                Destroy(gameObject);
                return;
            }
            float rise = layer.FadeIn ? Mathf.Clamp01(t / FadeIn) : 1f;
            source.volume = layer.Volume * rise * Mathf.Clamp01((length - t) / FadeOut);
        }
    }
}
