// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.11.0 at a0e33b9e74be2141e6ef38909d1d77de482cd7ac.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The mead affixes, all on the local player's own client: Swift Draught makes a healing mead heal its whole
    /// over-time amount at once; Brewer's Haste shortens the restoring status effect a mead adds, which is what blocks
    /// the next mead of its kind.
    /// A "healing mead" is a consumable whose status effect heals up front or over time (judgement call: resistance
    /// meads, whose status effect is the benefit itself, are never shortened).
    /// </summary>
    internal static class Meads
    {
        /// <summary>A status effect added while the local player drinks.</summary>
        public static void OnDrunk(SE_Stats effect)
        {
            if (!Restores(effect))
            {
                return;
            }
            AggregateValues v = AggregateHost.Current;
            if (v[EffectKind.MeadBurst] > 0f && effect.m_healthOverTimeTicks > 0f && effect.m_character != null)
            {
                effect.m_character.Heal(effect.m_healthOverTimeTicks * effect.m_healthOverTimeTickHP);
                effect.m_healthOverTimeTicks = 0f;
            }
            float shorter = v[EffectKind.MeadCooldown];
            if (shorter > 0f && effect.m_ttl > 0f)
            {
                float floor = Mathf.Max(effect.m_healthOverTimeDuration, Mathf.Max(effect.m_staminaOverTimeDuration, effect.m_eitrOverTimeDuration));
                effect.m_ttl = Mathf.Max(floor, effect.m_ttl * (1f - shorter));
            }
        }

        private static bool Restores(SE_Stats effect) =>
            effect.m_healthOverTime > 0f || effect.m_healthUpFront > 0f || effect.m_staminaOverTime > 0f
            || effect.m_staminaUpFront > 0f || effect.m_eitrOverTime > 0f || effect.m_eitrUpFront > 0f;
    }
}
