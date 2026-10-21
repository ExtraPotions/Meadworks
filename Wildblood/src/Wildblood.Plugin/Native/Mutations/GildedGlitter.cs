// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// The gold glitter a Gilded creature wears so players spot it from far off and give chase: the configured vanilla
    /// sparkle (<c>glitter effect</c>) cloned onto its body and made endless, plus a warm gold point light of the mod's
    /// own. It must read well past the nameplate, so every sparkle is held to a minimum size on screen - a distant glint
    /// stays a few pixels instead of shrinking to nothing - and the light pools gold around it. A one-shot sparkle is
    /// made a steady one: what the prefab authored as bursts is re-emitted evenly every cycle, thinned here by the
    /// player's effect density because the clone's own thinning reaches only continuous emission. A Gibber is disarmed
    /// before it can fling the clone apart and time it out, and the prefab's own lights, which fade and destroy
    /// themselves, give way to ours. Purely cosmetic, on every client (never on a dedicated server, which draws
    /// nothing); it rides the creature, so it moves and is destroyed with it. While a Cloaked creature is hidden, the
    /// glitter hides with it, or it would give the cloak away.
    /// </summary>
    public sealed class GildedGlitter : MonoBehaviour
    {
        /// <summary>Seconds over which one authored burst is spread when a one-shot sparkle is made steady.</summary>
        private const float Cycle = 2.5f;

        /// <summary>The smallest a sparkle may shrink on screen, as a share of the view: at 60 m a glint, not nothing.</summary>
        private const float MinScreenSize = 0.0025f;

        /// <summary>A man-sized creature's radius as the game measures it; the default sparkle is sized for a player.</summary>
        private const float AuthoredRadius = 0.85f;

        private const float LightRange = 8f;
        private const float LightIntensity = 2.5f;
        private static readonly Color Gold = new Color(1f, 0.78f, 0.32f);

        // Fallback when the configured name does not resolve: the nearest gold or yellow sparkle the game ships.
        private static readonly string[] Keywords = { "potion_stamina", "mead", "spark", "glow" };

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Light? _light;
        private CloakBehaviour? _cloak;
        private bool _shown = true;

        private void Start() => Guard.Run("GildedGlitter.Start", Build);

        private void Build()
        {
            _cloak = GetComponent<CloakBehaviour>();
            enabled = _cloak != null; // only a cloaked creature needs watching every frame
            Character character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            float density = Mathf.Clamp01(Configuration.EffectDensity.Value);
            if (character == null || controller == null || density <= 0f || IsHeadless())
            {
                return; // zero density spawns nothing, the light included
            }
            Vector3 center = character.GetCenterPoint();
            string effect = controller.Rules.PrefabOf(Mutation.Gilded, Fields.GlitterEffect);
            GameObject? prefab = EffectResolver.Resolve(effect, Keywords, "Gilded glitter effect");
            GameObject? clone = CosmeticClone.Spawn(prefab, transform, center, endless: true);
            if (clone != null)
            {
                Dress(clone, Mathf.Clamp(character.GetRadius() / AuthoredRadius, 0.75f, 3f), density);
            }
            _light = AddLight(center, density);
        }

        private static bool IsHeadless() => ZNet.instance != null && ZNet.instance.IsDedicated();

        private void Dress(GameObject clone, float scale, float density)
        {
            clone.transform.localScale *= scale;
            Disarm(clone);
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                MakeSteady(system, density);
            }
            foreach (ParticleSystemRenderer renderer in clone.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.minParticleSize = Mathf.Max(renderer.minParticleSize, MinScreenSize);
            }
            _renderers = clone.GetComponentsInChildren<Renderer>(true);
        }

        /// <summary>A Gibber is stopped before its Start can fling and time out the clone; the prefab's lights go.</summary>
        private static void Disarm(GameObject clone)
        {
            foreach (Gibber gibber in clone.GetComponentsInChildren<Gibber>(true))
            {
                gibber.enabled = false; // a disabled behaviour never gets its Start
                Destroy(gibber);
            }
            foreach (LightFlicker flicker in clone.GetComponentsInChildren<LightFlicker>(true))
            {
                Destroy(flicker);
            }
            foreach (Light light in clone.GetComponentsInChildren<Light>(true))
            {
                Destroy(light);
            }
        }

        /// <summary>Endless and steady: the authored bursts are re-emitted evenly over each cycle, thinned by density.</summary>
        private static void MakeSteady(ParticleSystem system, float density)
        {
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            ParticleSystem.EmissionModule emission = system.emission;
            float burst = BurstTotal(emission);
            if (burst > 0f)
            {
                float rate = emission.rateOverTimeMultiplier; // any continuous rate, already thinned by the clone
                emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                emission.rateOverTime = rate + burst / Cycle * density;
            }
            system.Play(withChildren: false);
        }

        private static float BurstTotal(ParticleSystem.EmissionModule emission)
        {
            float total = 0f;
            for (int i = 0; i < emission.burstCount; i++)
            {
                total += emission.GetBurst(i).count.constantMax;
            }
            return total;
        }

        private Light AddLight(Vector3 center, float density)
        {
            GameObject holder = new GameObject("ecr_gilded_light");
            holder.transform.SetParent(transform, worldPositionStays: false);
            holder.transform.position = center;
            GetComponent<EliteController>()?.OwnVisual(holder);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Gold;
            light.range = LightRange;
            light.intensity = LightIntensity * density;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Update() => Guard.Run("GildedGlitter.Update", static self => self.Step(), this);

        private void Step()
        {
            bool show = _cloak == null || !_cloak.Hidden;
            if (show == _shown)
            {
                return;
            }
            _shown = show;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = show;
                }
            }
            if (_light != null)
            {
                _light.enabled = show;
            }
        }
    }
}
