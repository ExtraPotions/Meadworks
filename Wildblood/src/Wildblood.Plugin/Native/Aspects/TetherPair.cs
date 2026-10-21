// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using ExtraPotions.Wildblood.Adopted.Rules;
using ExtraPotions.Wildblood.Adopted.Scaling;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Aspects
{
    /// <summary>
    /// What binds a Tethered pair: each boss's ZDO names the other (<see cref="AspectStore.GetTether"/>, written once, by the
    /// owner that brought the second one in), and the tether's strength is read from the two health bars alone - the share of its maximum
    /// each has left, which the game keeps in the ZDO - so every machine holding the pair measures the same gap without
    /// anything being sent. A partner this machine cannot see - dead, destroyed or not loaded here - counts as empty, so
    /// killing one of the two first leaves the other at the far end of the gap.
    /// </summary>
    internal static class TetherPair
    {
        /// <summary>The partner as this machine holds it; null when it is dead, destroyed or not loaded here.</summary>
        public static Character? Find(ZDOID partner)
        {
            if (partner == ZDOID.None || ZNetScene.instance == null)
            {
                return null;
            }
            GameObject? go = ZNetScene.instance.FindInstance(partner);
            Character? boss = go != null ? go.GetComponent<Character>() : null;
            return Alive(boss) ? boss : null;
        }

        /// <summary>The share of its maximum health a boss has left, as its ZDO holds it; 0 for one that is gone.</summary>
        public static float Fraction(Character? boss) => Alive(boss) ? Mathf.Clamp01(boss!.GetHealthPercentage()) : 0f;

        /// <summary>How taut the tether is, 0 to 1: the gap between the two shares over `full gap`.</summary>
        public static float Strength(float a, float b)
        {
            float full = Mathf.Max(0.01f, AspectMath.Power(Aspect.Tethered, Fields.FullGap) / 100f);
            return Mathf.Clamp01(Mathf.Abs(a - b) / full);
        }

        /// <summary>
        /// The partner is still standing, as its ZDO says on this machine: present, with health left (no health key means
        /// full). A partner whose ZDO is gone has fallen. Read where a boss dies, which may not have the partner loaded.
        /// </summary>
        public static bool Standing(ZDOID partner)
        {
            ZDO? other = partner != ZDOID.None && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(partner) : null;
            return other != null && other.IsValid() && other.GetFloat(ZDOVars.s_health, 1f) > 0f;
        }

        /// <summary>Still in the fight on this machine: loaded, its ZDO live, not dying.</summary>
        public static bool Alive(Character? boss) =>
            boss != null && boss.m_nview != null && boss.m_nview.IsValid() && !boss.IsDead();
    }
}
