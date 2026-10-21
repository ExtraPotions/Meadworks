// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what Flamebound's burning ground does to the player standing in it.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Mutations
{
    /// <summary>
    /// Sets this machine's own player on fire from a patch of Flamebound ground: one hit of pure fire damage with no
    /// attacker, which the game turns into its own Burning status (fire resistance and Fire Resistance Mead apply as to any
    /// other fire). Called by the player's own machine, which owns the player, so the hit lands locally.
    /// </summary>
    internal static class FireGround
    {
        public static void Ignite(Player player, float fire)
        {
            HitData hit = new HitData();
            hit.m_damage.m_fire = fire;
            hit.m_point = player.transform.position;
            hit.m_dir = Vector3.up;
            hit.m_hitType = HitData.HitType.Burning;
            player.Damage(hit);
        }
    }
}
