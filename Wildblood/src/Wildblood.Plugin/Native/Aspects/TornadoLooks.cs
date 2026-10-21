// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The pieces a Nightfall tornado is drawn and heard with. Its funnel wears a seamless churning cloud, its dust a
    /// sheet of torn wisps and its grit a small hard speck, all made here (<see cref="TornadoTextures"/>) and drawn
    /// unlit, so the funnel keeps its grey in the dark of a stormy night instead of going black. It howls with the game's
    /// own wind - the loop the world's wind plays - on a looped source of its own that follows it, heard in the world
    /// rather than everywhere, through the ambient volume like the world's wind, a little higher than the world's so it
    /// reads as its own.
    /// </summary>
    internal static class TornadoLooks
    {
        /// <summary>Where its wind is heard from, up its funnel; how near at full voice and how far at all; its pitch
        /// against the world's wind.</summary>
        private const float WindHeight = 3f;
        private const float WindNear = 6f;
        private const float WindFar = 60f;
        private const float WindPitch = 1.15f;

        private const string GlowShader = "Legacy Shaders/Particles/Alpha Blended";
        private const string PlainShader = "Sprites/Default";

        private static Material? _smoke;
        private static Material? _cloud;
        private static Material? _specks;

        /// <summary>The smoke material: the wisp sheet, <see cref="TornadoTextures.WispTiles"/> tiles each way; null with
        /// no particle shader.</summary>
        public static Material? Smoke() => _smoke != null ? _smoke : _smoke = Make(TornadoTextures.Wisps());

        /// <summary>The funnel's material: the seamless cloud its cones slide round and up.</summary>
        public static Material? Cloud() => _cloud != null ? _cloud : _cloud = Make(TornadoTextures.Cloud());

        /// <summary>The grit's material: small hard specks.</summary>
        public static Material? Specks() => _specks != null ? _specks : _specks = Make(TornadoTextures.Speck());

        private static Material? Make(Texture texture)
        {
            Shader? shader = Shader.Find(GlowShader) ?? Shader.Find(PlainShader);
            return shader != null ? new Material(shader) { mainTexture = texture } : null;
        }

        /// <summary>
        /// Its wind, on a source of its own under <paramref name="holder"/>: the world's wind loop, looped, heard
        /// <see cref="WindNear"/> metres off at full voice and fading to nothing by <see cref="WindFar"/>; silent until
        /// it is raised. Each tornado's pitch is its own, so two never drone in unison.
        /// </summary>
        public static AudioSource Wind(Transform holder)
        {
            GameObject voice = new GameObject("ecr_tornado_wind");
            voice.transform.SetParent(holder, false);
            voice.transform.localPosition = Vector3.up * WindHeight;
            AudioSource source = voice.AddComponent<AudioSource>();
            source.clip = AudioMan.instance != null ? AudioMan.instance.m_windAudio : null;
            source.outputAudioMixerGroup = AudioMan.instance != null ? AudioMan.instance.m_ambientMixer : null;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = WindNear;
            source.maxDistance = WindFar;
            source.dopplerLevel = 0f;
            source.pitch = Random.Range(WindPitch - 0.1f, WindPitch + 0.1f);
            source.volume = 0f;
            return source;
        }

    }
}
