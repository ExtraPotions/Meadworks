// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Damage taken by the local player. The game resolves a player's incoming hits on that player's own client
    /// (Character.RPC_Damage returns on non-owners), so every hook here filters to <c>Player.m_localPlayer</c> and runs
    /// there: nothing is sent except Bramblehide's returned hit, which goes to the attacker's owner by the game's own
    /// damage RPC.
    /// <para>
    /// Before the game's pipeline (prefix on RPC_Damage): Evader's Fury notices a melee hit passing through a dodge;
    /// Mist Veil discards the whole hit; the percent resists (by type, projectiles) and Steel Rhythm's window scale the
    /// hit, so they multiply with the game's categorical resists and armor that follow. After armor (ApplyDamage): the
    /// Runic Ward absorbs, and Bramblehide returns a share of what the player actually lost.
    /// </para>
    /// </summary>
    internal static class IncomingHits
    {
        /// <summary>The hit being resolved was blocked with an Anchored Guard shield (set in BlockAttack, cleared after).</summary>
        public static bool Steadfast;

        /// <summary>False = the hit is avoided and the game's RPC_Damage is skipped.</summary>
        public static bool OnIncoming(Player player, HitData hit)
        {
            Steadfast = false;
            AggregateValues v = AggregateHost.Current;
            bool fromCreature = hit.HaveAttacker();
            if (fromCreature && hit.m_dodgeable && !hit.m_ranged && player.IsDodgeInvincible())
            {
                CombatWindows.OnDodgedMelee();
                return true;
            }
            float avoid = v[EffectKind.AvoidHit];
            if (fromCreature && avoid > 0f && !player.IsDead() && Random.value < avoid)
            {
                DamageText.instance.ShowText(DamageText.TextType.Immune, hit.m_point, Text.Words.Localize("$ecf_fx_avoided"), player: true);
                return false;
            }
            Reduce(v, hit);
            return true;
        }

        private static void Reduce(AggregateValues v, HitData hit)
        {
            if (v.AnyDamageTaken)
            {
                DamageSlots.Reduce(ref hit.m_damage, v.DamageTaken);
            }
            float scale = 1f;
            if (hit.m_ranged)
            {
                scale *= 1f - v[EffectKind.RangedDamageTaken];
            }
            if (CombatWindows.RhythmActive)
            {
                scale *= 1f - v[EffectKind.ComboFinisher];
            }
            if (scale != 1f)
            {
                hit.m_damage.Modify(Mathf.Max(0f, scale));
            }
        }

        /// <summary>After armor, before health is lowered: the ward takes its share first.</summary>
        public static void BeforeHealthLoss(HitData hit)
        {
            float total = hit.GetTotalDamage();
            if (total <= 0f)
            {
                return;
            }
            float left = CombatWindows.OnDamaged(total);
            if (left < total)
            {
                hit.m_damage.Modify(left / total);
            }
        }

        /// <summary>
        /// Bramblehide: a melee hit from a creature returns X% of the health the player lost, as untyped damage from the
        /// player (Character.Damage routes it to the attacker's owner). Players are never hit back (judgement call:
        /// PvP stays vanilla).
        /// </summary>
        public static void AfterHealthLoss(Player player, HitData hit, float lost)
        {
            float share = AggregateHost.Current[EffectKind.Thorns];
            Character? attacker = share > 0f && lost > 0f && !hit.m_ranged && hit.m_hitType == HitData.HitType.EnemyHit ? hit.GetAttacker() : null;
            if (attacker == null || attacker.IsPlayer() || attacker.IsDead())
            {
                return;
            }
            HitData back = new HitData();
            back.m_damage.m_damage = lost * share;
            back.m_point = attacker.GetCenterPoint();
            back.m_dir = (attacker.transform.position - player.transform.position).normalized;
            back.m_hitType = HitData.HitType.PlayerHit;
            back.SetAttacker(player);
            attacker.Damage(back);
        }
    }
}
