// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Config;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// The vanilla pieces a Stormbound storm is drawn and heard with. The strike is the Himminafl axe's own lightning
    /// strike, authored for a 2.6 m area: a bolt, a burst of stone and dust, a blue flash, a crack of electricity and a
    /// camera shake that reaches only 7 m and is mild. It is cloned through <see cref="CosmeticClone"/>, so the
    /// player's effect density applies, sized to the circle, and restarted so its bolt - authored half a second late -
    /// falls on the instant the strike lands. Over the strikes, one thunderclap from the Thunderblood staff. The
    /// circle's line borrows the same effect's lightning trail material: a soft, bright band. The aspects block of the
    /// rule file holds numbers only, so these names are code constants rather than rule fields.
    /// </summary>
    internal static class StormEffects
    {
        public const string StrikeEffect = "fx_himminafl_aoe";
        public const string ThunderSound = "sfx_staffthunderblood_thunder";

        /// <summary>The radius the strike effect is authored for (its bolt's emission cone).</summary>
        private const float StrikeRadius = 2.6f;

        private static readonly string[] StrikeKeywords = { "lightning", "himminafl", "spark" };
        private static readonly string[] ThunderKeywords = { "thunder", "lightning" };

        private static Material? _ringMaterial;

        /// <summary>The strike at one circle, under <paramref name="holder"/> so it goes with the circle.</summary>
        public static void Strike(Transform holder, Vector3 center, float radius)
        {
            GameObject? prefab = EffectResolver.Resolve(StrikeEffect, StrikeKeywords, "Stormbound strike");
            GameObject? clone = CosmeticClone.Spawn(prefab, holder, center, endless: false);
            if (clone == null)
            {
                return;
            }
            Restart(clone);
            clone.transform.localScale *= Mathf.Max(0.2f, radius / StrikeRadius);
        }

        // Every system starts again with no delay, so the bolt falls with the burst, and follows the root's scale.
        private static void Restart(GameObject clone)
        {
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.startDelay = 0f;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                system.Play(false);
            }
        }

        /// <summary>One thunderclap per storm; heard whatever the effect density, which governs what is seen.</summary>
        public static void Thunder(Vector3 at) =>
            CosmeticClone.Sound(EffectResolver.ResolveSound(ThunderSound, ThunderKeywords, "Stormbound thunder"), at);

        /// <summary>The lightning trail material the circle is drawn with; null if the effect is missing.</summary>
        public static Material? RingMaterial()
        {
            if (_ringMaterial != null)
            {
                return _ringMaterial;
            }
            GameObject? prefab = EffectResolver.Resolve(StrikeEffect, StrikeKeywords, "Stormbound strike");
            if (prefab == null)
            {
                return null;
            }
            foreach (ParticleSystemRenderer renderer in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (renderer.trailMaterial != null)
                {
                    return _ringMaterial = renderer.trailMaterial;
                }
            }
            return null;
        }

        public static float Density() => Mathf.Clamp01(Configuration.EffectDensity.Value);

        /// <summary>A dedicated server has no screen or local player: it draws and judges nothing.</summary>
        public static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
