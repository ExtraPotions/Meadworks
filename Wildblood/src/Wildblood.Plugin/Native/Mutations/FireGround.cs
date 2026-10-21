// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what Flamebound's burning ground does to the player standing in it.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Sets this machine's own player on fire from a patch of Flamebound ground. Fire resistance and being wet still
    /// apply, through the game's own Burning. Armor does not: a hit of this size was stopped by about 9 armor before it
    /// could start Burning. No attacker, so it is not scaled again and does not feed Leeching. The player's own machine
    /// decides it.
    /// </summary>
    internal static class FireGround
    {
        public static void Ignite(Player player, float fire)
        {
            if (player == null || fire <= 0f) return;
            HitData hit = new HitData();
            hit.m_damage.m_fire = fire;
            hit.ApplyResistance(player.GetDamageModifiers(), out _);
            if (hit.m_damage.m_fire <= 0f) return;
            player.AddFireDamage(hit.m_damage.m_fire, hit.m_variant);
        }
    }
}
