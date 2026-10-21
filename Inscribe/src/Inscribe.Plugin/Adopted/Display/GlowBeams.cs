// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Bundles;
using ExtraPotions.Inscribe.Adopted.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtraPotions.Inscribe.Adopted.Display
{
    /// <summary>
    /// The loot beam (display.md section 5, the user's pick "C" of the 2026-10-05 previews): a soft shaft of light about
    /// 3.7 m tall with sparks rising round its foot, from the embedded bundle <c>ecf_lootglow</c> (ValheimAssets
    /// <c>Assets/Effects/ecf_lootglow_beam_motes</c>), dressed in the game's particle shaders (<c>BundleEffects</c>).
    /// Its five systems are authored in Magic green; one recoloured copy is kept per glow colour (<see cref="Tint"/>),
    /// so an instance starts in its own colour (<see cref="ParticleRetint"/>). Local only: no ZNetView, nothing sent, never on a headless server.
    /// </summary>
    internal static class GlowBeams
    {
        public const string Bundle = "ecf_lootglow";
        public const string ChildName = "ecf_lootbeam";
        public const string GlintChildName = "Inscribe_enhancement_glint";
        private const string Effect = "ecf_lootglow_beam_motes";

        private static readonly Dictionary<int, GameObject> Templates = new Dictionary<int, GameObject>();
        private static GameObject? _effect;
        private static GameObject? _glint;
        private static bool _tried;

        /// <summary>A beam in this colour on the item, upright and unscaled; null when the effect cannot load.</summary>
        public static GameObject? Create(Transform item, Color32 colour)
        {
            GameObject? template = Template(colour);
            if (template == null)
            {
                return null;
            }
            GameObject beam = Object.Instantiate(template, item, false);
            beam.name = ChildName;
            Upright(beam.transform);
            return beam;
        }

        public static GameObject? CreateGlint(Transform item)
        {
            if (_glint == null)
            {
                Transform? sparks = Prepared()?.transform.Find("sparks");
                if (sparks == null)
                {
                    return null;
                }
                // Copy only the dressed motes: enhancement must never inherit the rarity shaft or halo.
                _glint = PrefabBench.Copy(sparks.gameObject, GlintChildName + "_template");
                ParticleSystem system = _glint.GetComponent<ParticleSystem>();
                ParticleSystem.MainModule main = system.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color32(195, 157, 97, 210), new Color32(243, 232, 208, 235));
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
                main.gravityModifier = 0f;
                main.maxParticles = 4;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 1.5f;
                ParticleSystem.ShapeModule shape = system.shape;
                shape.scale = new Vector3(0.3f, 0.3f, 0.025f);
            }
            GameObject glint = Object.Instantiate(_glint, item, false);
            glint.name = GlintChildName;
            glint.transform.localPosition = Vector3.up * 0.08f;
            Upright(glint.transform);
            return glint;
        }

        /// <summary>Stands the beam straight up at normal size, however the item lies or is scaled.</summary>
        public static void Upright(Transform beam)
        {
            if (beam.rotation != Quaternion.identity)
            {
                beam.rotation = Quaternion.identity;
            }
            float scale = beam.parent != null ? Mathf.Max(beam.parent.lossyScale.x, 0.001f) : 1f;
            Vector3 local = Vector3.one / scale;
            if (beam.localScale != local)
            {
                beam.localScale = local;
            }
        }

        private static GameObject? Template(Color32 colour)
        {
            int key = (colour.r << 16) | (colour.g << 8) | colour.b;
            if (Templates.TryGetValue(key, out GameObject template) && template != null)
            {
                return template;
            }
            GameObject? effect = Prepared();
            if (effect == null)
            {
                return null;
            }
            template = PrefabBench.Copy(effect, $"{Effect}_{key:X6}");
            ParticleRetint.Tint(template, colour);
            Templates[key] = template;
            return template;
        }

        /// <summary>The dressed effect, made once after the game's scene (and its shaders) is up; null on failure.</summary>
        private static GameObject? Prepared()
        {
            if (_effect == null && !ReferenceEquals(_effect, null))
            {
                _tried = false;
            }
            if (_tried || ZNetScene.instance == null)
            {
                return _effect;
            }
            _tried = true;
            try
            {
                _effect = BundleEffects.Prepare(EmbeddedBundle.Load(typeof(GlowBeams).Assembly, Bundle), Effect);
            }
            catch (Exception e)
            {
                Log.Warn($"the loot beam did not load, dropped items keep only their light: {e.Message}");
            }
            return _effect;
        }
    }
}
