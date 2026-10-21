// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Runtime;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using ExtraPotions.Wildblood.Adopted.Visuals;
using ExtraPotions.Wildblood.AdoptedLibs.PatchGuard;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// What Frostbound's aura looks like, so a player sees the cold before they feel it: the configured vanilla frost
    /// (<c>aura effect</c>, the pale mist and glints a frost-slowed character wears) cloned onto the creature's body,
    /// sized to it as the game sizes a worn status effect and made endless, plus a pale ice-blue point light of the mod's
    /// own whose reach is the aura's, so at night the chilled ground shows. Purely cosmetic, on every client (never on a
    /// dedicated server, which draws nothing); it rides the creature, so it moves and is destroyed with it. While a
    /// Cloaked creature is hidden, the aura hides with it, or it would give the cloak away.
    /// </summary>
    public sealed class FrostAuraLook : MonoBehaviour
    {
        private const float LightIntensity = 1.2f;

        /// <summary>The game's own frost light colour (its ice hits and frost arrows).</summary>
        private static readonly Color Ice = new Color(0.455f, 0.842f, 1f);

        // Fallback when the configured name does not resolve: the nearest frost or cold effect the game ships.
        private static readonly string[] Keywords = { "vfx_frost", "frost", "cold", "freez", "ice" };

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private Light? _light;
        private CloakBehaviour? _cloak;
        private bool _shown = true;
        private Action? _step;

        private void Start() => Guard.Run("FrostAuraLook.Start", Build);

        private void Build()
        {
            _cloak = GetComponent<CloakBehaviour>();
            enabled = _cloak != null; // only a cloaked creature needs watching every frame
            Character character = GetComponent<Character>();
            EliteController controller = GetComponent<EliteController>();
            float density = Mathf.Clamp01(Configuration.EffectDensity.Value);
            if (character == null || controller == null || density <= 0f)
            {
                return; // zero density spawns nothing, the light included
            }
            Vector3 center = character.GetCenterPoint();
            string effect = controller.Rules.PrefabOf(Mutation.Frostbound, Fields.AuraEffect);
            GameObject? prefab = EffectResolver.Resolve(effect, Keywords, "Frostbound aura effect");
            GameObject? clone = CosmeticClone.Spawn(prefab, transform, center, endless: true);
            if (clone != null)
            {
                Dress(clone, Mathf.Max(0.5f, character.GetRadius() * 2f));
            }
            float reach = Enhance.Magnitude(controller.Rules, controller.Traits, Mutation.Frostbound, Fields.AuraRadius);
            _light = AddLight(center, reach, density);
        }

        /// <summary>Sized as the game sizes a worn status effect (twice the body's radius) and looped for good.</summary>
        private void Dress(GameObject clone, float scale)
        {
            clone.transform.localScale *= scale;
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                system.Play(withChildren: false);
            }
            _renderers = clone.GetComponentsInChildren<Renderer>(true);
        }

        private Light? AddLight(Vector3 center, float reach, float density)
        {
            if (reach <= 0f)
            {
                return null; // no aura, nothing to light
            }
            GameObject holder = new GameObject("ecr_frost_light");
            holder.transform.SetParent(transform, worldPositionStays: false);
            holder.transform.position = center;
            GetComponent<EliteController>()?.OwnVisual(holder);
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = Ice;
            light.range = Mathf.Min(reach, 30f);
            light.intensity = LightIntensity * density;
            light.shadows = LightShadows.None;
            return light;
        }

        private void Update() => Guard.Run("FrostAuraLook.Update", _step ??= Step);

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
