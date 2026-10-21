// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The particle half of an Adaptive boss's glow: the game's own potion aura - rising trails, sparkles and a soft
    /// halo, authored to sit around a body - cloned locally onto the boss, made endless, sized to it, and stripped of
    /// its colour so it can wear any. Every system is tinted through its start colour (keeping the authored alpha),
    /// with its colour-over-life and trails whitened, so a recolour is one start-colour change: particles already in
    /// the air keep the old colour until they fade, which crossfades the aura in a second or two. It starts dark and
    /// stops emitting while nothing is resisted. The clone is thinned by the player's effect density and parented to
    /// the boss, so it dies with it.
    /// </summary>
    internal sealed class AdaptiveAura
    {
        /// <summary>A vanilla aura made to wrap a body; if an update renames it, the nearest potion aura.</summary>
        private const string AuraName = "vfx_Potion_health_medium";
        private static readonly string[] Keywords = { "potion", "mead" };

        private readonly ParticleSystem[] _systems;
        private readonly float[] _alphas;

        private AdaptiveAura(ParticleSystem[] systems)
        {
            _systems = systems;
            _alphas = new float[systems.Length];
            for (int i = 0; i < systems.Length; i++)
            {
                _alphas[i] = AlphaOf(systems[i].main.startColor);
                Prepare(systems[i]);
            }
        }

        /// <summary>The aura on the boss at <paramref name="scale"/> times its authored size; null if off.</summary>
        public static AdaptiveAura? Build(Transform parent, Vector3 center, float scale)
        {
            GameObject? prefab = EffectResolver.Resolve(AuraName, Keywords, "Adaptive glow");
            GameObject? clone = CosmeticClone.Spawn(prefab, parent, center, endless: true);
            if (clone == null)
            {
                return null;
            }
            foreach (Light light in clone.GetComponentsInChildren<Light>(true))
            {
                Object.Destroy(light); // the prefab's own light has the potion's colour; the glow brings its own
            }
            AdaptiveAura aura = new AdaptiveAura(clone.GetComponentsInChildren<ParticleSystem>(true));
            clone.transform.localScale *= scale;
            return aura;
        }

        /// <summary>Tints every system to the colour, or stops them emitting when there is none.</summary>
        public void Show(Color? colour)
        {
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem system = _systems[i];
                if (system == null)
                {
                    continue;
                }
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = colour.HasValue;
                if (colour.HasValue)
                {
                    ParticleSystem.MainModule main = system.main;
                    main.startColor = new Color(colour.Value.r, colour.Value.g, colour.Value.b, _alphas[i]);
                }
            }
        }

        /// <summary>Endless, scaled with the root, white, and dark until the first type is shown.</summary>
        private static void Prepare(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // every child system takes the boss-sized scale
            ParticleSystem.ColorOverLifetimeModule overLife = system.colorOverLifetime;
            if (overLife.enabled)
            {
                overLife.color = White(overLife.color);
            }
            WhitenTrails(system.trails);
            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            system.Play(withChildren: false);
        }

        private static void WhitenTrails(ParticleSystem.TrailModule trails)
        {
            if (!trails.enabled)
            {
                return;
            }
            trails.inheritParticleColor = true;
            trails.colorOverLifetime = White(trails.colorOverLifetime);
            trails.colorOverTrail = White(trails.colorOverTrail);
        }

        /// <summary>The same fade with the colour taken out: white keys over the authored alpha keys.</summary>
        private static ParticleSystem.MinMaxGradient White(ParticleSystem.MinMaxGradient source)
        {
            Gradient? gradient = source.mode switch
            {
                ParticleSystemGradientMode.Gradient => source.gradient,
                ParticleSystemGradientMode.RandomColor => source.gradient,
                ParticleSystemGradientMode.TwoGradients => source.gradientMax,
                _ => null,
            };
            if (gradient == null)
            {
                return new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, AlphaOf(source)));
            }
            Gradient white = new Gradient();
            GradientColorKey[] keys = { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) };
            white.SetKeys(keys, gradient.alphaKeys);
            return new ParticleSystem.MinMaxGradient(white);
        }

        private static float AlphaOf(ParticleSystem.MinMaxGradient colour) => colour.mode switch
        {
            ParticleSystemGradientMode.Color => colour.color.a,
            ParticleSystemGradientMode.TwoColors => colour.colorMax.a,
            _ => 1f,
        };
    }
}
