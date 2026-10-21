// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Guard/Thorns.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// Thorns: a held block of a melee hit sends Thorns percent of the damage the block stopped back to the attacker, as
    /// pierce damage from the blocker. It is an ordinary hit sent through Character.Damage, which reaches the attacker's
    /// owner by RPC; it cannot be blocked or dodged, and the game's PvP rules apply to player attackers. It trains no
    /// skill. Sent from the blocker's client.
    /// </summary>
    public static class Thorns
    {
        private const float Smallest = 0.5f;

        public static void OnBlock(Player player, Character attacker, HitData hit, float stopped)
        {
            if (attacker == null || attacker == player || attacker.IsDead() || hit.m_ranged)
                return;
            float damage = stopped * DefenseSkill.LocalShare(DefenseGuardSettings.Thorns.Value);
            if (damage < Smallest)
                return;
            attacker.Damage(Back(player, attacker, damage));
        }

        private static HitData Back(Player player, Character attacker, float damage)
        {
            HitData back = new HitData();
            back.m_damage.m_pierce = damage;
            back.m_point = attacker.GetCenterPoint();
            back.m_dir = (attacker.transform.position - player.transform.position).normalized;
            back.m_blockable = false;
            back.m_dodgeable = false;
            back.m_hitType = HitData.HitType.PlayerHit;
            back.SetAttacker(player);
            return back;
        }
    }
}
