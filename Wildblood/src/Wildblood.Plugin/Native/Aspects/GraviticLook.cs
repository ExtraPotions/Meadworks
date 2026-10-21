// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using ExtraPotions.Wildblood.Adopted.Visuals;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What a machine holding a Gravitic boss sees and hears, each a local cosmetic clone drawn from the roar message
    /// (<see cref="CosmeticClone"/>, so the player's effect density applies). The roar is the boss's own alert cry - the
    /// sound a player already knows as "it has seen me" - with a pulse at its feet and a tremor for anyone in the pull's
    /// reach; the slam is a ground-slam burst drawn to the slam's reach, its thud, and a harder shake. A dedicated server
    /// has no screen or speakers: it draws nothing.
    /// </summary>
    internal static class GraviticLook
    {
        private const string PulseEffect = "fx_Fader_Roar";
        private const string CrySound = "sfx_gdking_scream";
        private const string SlamEffect = "vfx_troll_groundslam";
        private const string SlamSound = "sfx_gdking_stomp";

        /// <summary>Metres beyond the body the roar's pulse is drawn to.</summary>
        private const float PulseReach = 3f;

        private const float RoarShake = 0.5f;
        private const float SlamShake = 1.5f;

        private static readonly string[] Pulse = { "roar", "nova", "shockwave", "stomp" };
        private static readonly string[] Cry = { "scream", "taunt", "alert" };
        private static readonly string[] Burst = { "groundslam", "stomp", "slam" };
        private static readonly string[] Thud = { "stomp", "slam", "impact" };

        public static void Roar(Character boss, Vector3 anchor, float body, float range)
        {
            if (Headless())
            {
                return;
            }
            Voice(boss);
            CosmeticClone.Flash(EffectResolver.Resolve(PulseEffect, Pulse, "Gravitic roar"), anchor, body + PulseReach);
            Shake(anchor, range, RoarShake);
        }

        public static void Slam(Vector3 anchor, float reach)
        {
            if (Headless())
            {
                return;
            }
            CosmeticClone.FlashWhole(EffectResolver.Resolve(SlamEffect, Burst, "Gravitic slam"), anchor, reach, 1f);
            CosmeticClone.Sound(EffectResolver.ResolveSound(SlamSound, Thud, "Gravitic slam sound"), anchor);
            Shake(anchor, reach * 2f, SlamShake);
        }

        // The boss's own alert cry: every vanilla boss has one. Only a boss without one gets a stock scream instead.
        private static void Voice(Character boss)
        {
            BaseAI ai = boss.GetBaseAI();
            EffectList.EffectData[] cries = ai != null && ai.m_alertedEffects?.m_effectPrefabs != null
                ? ai.m_alertedEffects.m_effectPrefabs
                : Array.Empty<EffectList.EffectData>();
            bool heard = false;
            foreach (EffectList.EffectData cry in cries)
            {
                if (cry != null && cry.m_enabled && cry.m_prefab != null && cry.m_prefab.GetComponentInChildren<ZSFX>(true) != null)
                {
                    CosmeticClone.Sound(cry.m_prefab, boss.transform.position);
                    heard = true;
                }
            }
            if (!heard)
            {
                CosmeticClone.Sound(EffectResolver.ResolveSound(CrySound, Cry, "Gravitic roar sound"), boss.transform.position);
            }
        }

        private static void Shake(Vector3 at, float range, float strength)
        {
            if (GameCamera.instance != null && range > 0f)
            {
                GameCamera.instance.AddShake(at, range, strength, continous: false);
            }
        }

        private static bool Headless() => ZNet.instance != null && ZNet.instance.IsDedicated();
    }
}
