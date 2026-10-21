// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Visuals
{
    /// <summary>
    /// Keeps a cloned effect visible for exactly as long as the hazard it marks, then thins it away in the last second
    /// so a player can time a move through. It loops the clone's particles rather than re-spawning them, so one cloud
    /// looks like one cloud however long it lives, and scales the clone so its visible size matches the hazard radius.
    /// It rides on the hazard's own GameObject, so when the hazard is destroyed the effect goes with it, cleanly. A held
    /// visual never thins: it is a warning that ends in something else, and lasts until its host is destroyed.
    /// </summary>
    public sealed class LingeringVisual : MonoBehaviour
    {
        /// <summary>The authored radius of a typical vanilla ground effect; the clone is scaled up or down from this.</summary>
        private const float BaselineRadius = 4f;

        /// <summary>The window over which the cloud thins before it is gone - "the last second or so" the spec asks for.</summary>
        private const float FadeSeconds = 1f;

        private ParticleSystem[] _systems = Array.Empty<ParticleSystem>();
        private Light[] _lights = Array.Empty<Light>();
        private AudioSource[] _audio = Array.Empty<AudioSource>();
        private float[] _lightBase = Array.Empty<float>();
        private float[] _audioBase = Array.Empty<float>();
        private float _life;
        private float _age;
        private bool _fading;

        /// <summary>Attaches a lasting effect to a hazard; a null or off prefab simply leaves the hazard bare.</summary>
        public static void Attach(GameObject host, GameObject? prefab, float life, float radius)
        {
            LingeringVisual visual = host.AddComponent<LingeringVisual>();
            visual._life = Mathf.Max(life, 0.01f);
            Guard.Run("LingeringVisual.Build", () => visual.Build(host, prefab, radius));
        }

        /// <summary>
        /// Attaches an effect that stays at full strength until its host is destroyed - for a warning that ends in
        /// something else (the Bloated blast), which must not thin out beforehand as if the danger were passing.
        /// </summary>
        public static void Hold(GameObject host, GameObject? prefab, float radius) =>
            Attach(host, prefab, float.PositiveInfinity, radius); // an endless life never reaches its fade

        private void Build(GameObject host, GameObject? prefab, float radius)
        {
            GameObject? clone = CosmeticClone.Spawn(prefab, host.transform, host.transform.position, endless: true);
            if (clone == null)
            {
                enabled = false;
                return;
            }
            clone.transform.localScale *= Mathf.Max(radius, 0.01f) / BaselineRadius;
            Loop(clone);
            Capture(clone);
        }

        private void Loop(GameObject clone)
        {
            _systems = clone.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem system in _systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                system.Play(withChildren: true);
            }
        }

        private void Capture(GameObject clone)
        {
            _lights = clone.GetComponentsInChildren<Light>(true);
            _audio = clone.GetComponentsInChildren<AudioSource>(true);
            _lightBase = Baseline(_lights, l => l.intensity);
            _audioBase = Baseline(_audio, a => a.volume);
        }

        private static float[] Baseline<T>(T[] items, Func<T, float> read)
        {
            float[] values = new float[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                values[i] = read(items[i]);
            }
            return values;
        }

        private void Update() => Guard.Run("LingeringVisual.Update", static self => self.Step(), this);

        private void Step()
        {
            _age += Time.deltaTime;
            if (!_fading && _age >= _life - FadeSeconds)
            {
                BeginFade();
            }
            if (_fading)
            {
                Dim(Mathf.Clamp01((_life - _age) / FadeSeconds));
            }
        }

        private void BeginFade()
        {
            _fading = true;
            foreach (ParticleSystem system in _systems)
            {
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = false;
            }
        }

        private void Dim(float factor)
        {
            for (int i = 0; i < _lights.Length; i++)
            {
                if (_lights[i] != null)
                {
                    _lights[i].intensity = _lightBase[i] * factor;
                }
            }
            for (int i = 0; i < _audio.Length; i++)
            {
                if (_audio[i] != null)
                {
                    _audio[i].volume = _audioBase[i] * factor;
                }
            }
        }
    }
}
