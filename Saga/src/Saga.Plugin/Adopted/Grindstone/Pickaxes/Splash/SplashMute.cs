// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Pickaxes/Splash/SplashMute.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Splash lands quietly, on the rock's ZDO owner. While <see cref="DamageArea"/> applies a splash hit to a chunk,
    /// the game's feedback for a damaged chunk is held back:
    /// <list type="bullet">
    /// <item><b>The hit effect</b> (MineRock5.m_hitEffect, the chip sound and dust): the rock's list is swapped for an
    /// empty one for the call and put back in a finally.</item>
    /// <item><b>The damage number</b> (DamageText.ShowText, a routed RPC that shows it to everybody near): a prefix on
    /// the overload every ShowText ends in skips it.</item>
    /// <item><b>The noise</b> (Character.AddNoise(100) on the closest player within 10 m, which alerts creatures): the hit
    /// that splashed has just added the same noise at the same spot, and noise keeps the largest range rather than adding
    /// up, so each splashed chunk's call would only cost an RPC when another machine owns that player.</item>
    /// </list>
    /// The destroyed effect of a chunk that breaks (the crumble), its drops and the whole break dispatch run as usual:
    /// nothing is held back while <see cref="MineBreak"/> dispatches a break, so a feature's own text or sound stays.
    /// Collapses from the support check afterwards are the game's own and keep their feedback.
    /// </summary>
    public static class SplashMute
    {
        /// <summary>Put in a rock's m_hitEffect while splash damages it: no prefabs, so Create spawns nothing.</summary>
        private static readonly EffectList Silent = new EffectList();

        private static int depth;

        /// <summary>The game's hit feedback is held back right now: inside a splash's DamageArea, outside any break dispatch.</summary>
        public static bool Muting => depth > 0 && MineBreak.Open == null;

        [HarmonyPatch(typeof(DamageText), nameof(DamageText.ShowText), typeof(DamageText.TextType), typeof(Vector3), typeof(string), typeof(bool))]
        private static class QuietNumber
        {
            [HarmonyPrefix]
            private static bool Prefix() => !Muting;
        }

        [HarmonyPatch(typeof(Character), nameof(Character.AddNoise))]
        private static class QuietNoise
        {
            [HarmonyPrefix]
            private static bool Prefix() => !Muting;
        }

        /// <summary>
        /// MineRock5.DamageArea(<paramref name="area"/>, <paramref name="splash"/>) with the hit effect, damage number and
        /// noise held back; true when the chunk broke. The rock's own hit effect is always back in place afterwards.
        /// </summary>
        public static bool DamageArea(MineRock5 chunks, int area, HitData splash)
        {
            EffectList hitEffect = chunks.m_hitEffect;
            chunks.m_hitEffect = Silent;
            depth++;
            try
            {
                return chunks.DamageArea(area, splash);
            }
            finally
            {
                depth--;
                chunks.m_hitEffect = hitEffect;
            }
        }
    }
}
