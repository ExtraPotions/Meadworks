// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Config;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What an Adaptive boss shows every client: a glow in the colour of the damage type it resists, so a player sees
    /// at a glance what not to hit it with. Two parts, both parented to the boss so they move and die with it: the
    /// recoloured particle aura (<see cref="AdaptiveAura"/>) and a point light of the mod's own that pools the same
    /// colour over the boss and the ground around it, which reads even through a crowd. On a change the light blends
    /// to the new colour in about half a second and the aura crossfades as its particles turn over; with nothing
    /// resisted both go dark. Brightness and particle count follow the player's effect density, and zero density - or a
    /// dedicated server, which draws nothing - builds nothing at all.
    /// </summary>
    internal sealed class AdaptiveGlow
    {
        /// <summary>A man-sized creature's radius as the game measures it; the aura is authored for one.</summary>
        private const float AuthoredRadius = 0.85f;

        /// <summary>The largest the aura grows, so the biggest boss wears a glow rather than a fog bank.</summary>
        private const float MaxScale = 6f;

        private const float LightIntensity = 3f;
        private const float LightRange = 6f;

        /// <summary>How fast the light's colour closes on a new one, per second: about half a second.</summary>
        private const float Blend = 6f;

        /// <summary>
        /// One clear colour per type, indexed by its value: fire orange-red, frost ice blue, lightning violet, poison
        /// green, spirit pale white; blunt yellow, slash pink and pierce teal, so no two neighbours share a hue.
        /// </summary>
        private static readonly Color[] Colours =
        {
            Color.black,                    // None: never shown
            new Color(1.00f, 0.90f, 0.25f), // Blunt
            new Color(1.00f, 0.25f, 0.70f), // Slash
            new Color(0.10f, 0.95f, 0.80f), // Pierce
            new Color(1.00f, 0.38f, 0.08f), // Fire
            new Color(0.45f, 0.75f, 1.00f), // Frost
            new Color(0.62f, 0.30f, 1.00f), // Lightning
            new Color(0.35f, 0.95f, 0.20f), // Poison
            new Color(0.92f, 0.95f, 1.00f), // Spirit
        };

        private readonly Light _light;
        private readonly float _brightness;
        private readonly AdaptiveAura? _aura;
        private AdaptiveType _shown = AdaptiveType.None;

        private AdaptiveGlow(Light light, float brightness, AdaptiveAura? aura)
        {
            _light = light;
            _brightness = brightness;
            _aura = aura;
        }

        /// <summary>The glow for this boss on this client; null on a dedicated server or with effects off.</summary>
        public static AdaptiveGlow? Create(Character boss)
        {
            float density = Mathf.Clamp01(Configuration.EffectDensity.Value);
            if (density <= 0f || (ZNet.instance != null && ZNet.instance.IsDedicated()))
            {
                return null;
            }
            float scale = Mathf.Clamp(boss.GetRadius() / AuthoredRadius, 1f, MaxScale);
            Vector3 center = boss.GetCenterPoint();
            AdaptiveAura? aura = AdaptiveAura.Build(boss.transform, center, scale);
            return new AdaptiveGlow(AddLight(boss.transform, center, scale), LightIntensity * density, aura);
        }

        /// <summary>Per frame: recolours on a change and eases the light to the type's colour and brightness.</summary>
        public void Tick(AdaptiveType type)
        {
            if (type != _shown)
            {
                _shown = type;
                _aura?.Show(type == AdaptiveType.None ? (Color?)null : Colours[(int)type]);
            }
            if (_light == null)
            {
                return;
            }
            bool on = type != AdaptiveType.None;
            if (on)
            {
                // From dark it lights straight in the new colour; lit, it blends across from the old one.
                Color target = Colours[(int)type];
                bool dark = _light.intensity <= 0.01f;
                _light.color = dark ? target : Color.Lerp(_light.color, target, Time.deltaTime * Blend);
            }
            float rate = _brightness * 2f; // half a second to light up or go dark
            _light.intensity = Mathf.MoveTowards(_light.intensity, on ? _brightness : 0f, rate * Time.deltaTime);
        }

        private static Light AddLight(Transform parent, Vector3 center, float scale)
        {
            GameObject holder = new GameObject("ecr_adaptive_light");
            holder.transform.SetParent(parent, worldPositionStays: false);
            holder.transform.position = center;
            Light light = holder.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = LightRange + 2f * scale;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            return light;
        }
    }
}
