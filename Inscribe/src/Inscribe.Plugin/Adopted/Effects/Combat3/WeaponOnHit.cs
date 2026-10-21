// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Effects.Combat3
{
    /// <summary>
    /// The item-local on-hit effects of the weapon behind a local player's hit (<see cref="LocalHits.Weapon"/>), on the
    /// attacker's client while the hit is built; everything rides in the HitData to the target's owner:
    /// <list type="bullet">
    /// <item><c>knockback_dealt</c> (Mighty Blows): the hit's push force is X% larger; the owner pushes the target with
    /// it (Character.ApplyPushback), melee, projectile and area hits alike.</item>
    /// <item><c>heavy_hand</c> (Heavy Hand): the hit's stagger multiplier is X% larger; the owner adds the stagger with it
    /// (Character.AddStaggerDamage). The swing's slowdown is <see cref="SwingSpeed"/>'s.</item>
    /// <item><c>paralyze</c> (Numbing Blow): the hit carries <see cref="EcfParalyze"/> and its length
    /// (<see cref="Paralysis.Carry"/>).</item>
    /// <item><c>chain_lightning</c> (Thor's Chain): on its chance, the arcs follow once the hit itself has gone out
    /// (<see cref="ChainLightning"/>).</item>
    /// </list>
    /// Low priority: after the critical roll and the Phase 2 outgoing bonuses, so an arc takes its share of the final hit.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class WeaponOnHitPatch
    {
        [HarmonyPriority(Priority.Low)]
        private static void Prefix(Character __instance, HitData hit, out float __state)
        {
            __state = 0f;
            Player? player = LocalHits.Attacker(hit);
            if (player != null && !ReferenceEquals(__instance, player) && !__instance.IsDead())
            {
                __state = Apply(player, __instance, hit);
            }
        }

        [HarmonyPriority(Priority.Low)]
        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (__state > 0f)
            {
                ChainLightning.Arc(__instance, hit.m_skill, __state);
            }
        }

        // Returns the lightning damage each arc deals, 0 when the chain does not fire.
        private static float Apply(Player player, Character target, HitData hit)
        {
            ItemLocalSums? sums = ItemLocalCache.Get(LocalHits.Weapon(player));
            if (sums == null)
            {
                return 0f;
            }
            Weight(sums, hit);
            Paralysis.Carry(sums.Get(EffectKind.Paralyze), target, hit);
            return ChainLightning.Proc(sums.Get(EffectKind.ChainLightning), player, target, hit);
        }

        // Mighty Blows pushes further, Heavy Hand staggers more.
        private static void Weight(ItemLocalSums sums, HitData hit)
        {
            float push = sums.Get(EffectKind.KnockbackDealt);
            if (push > 0f)
            {
                hit.m_pushForce *= 1f + push;
            }
            float stagger = sums.Get(EffectKind.HeavyHand);
            if (stagger > 0f)
            {
                hit.m_staggerMultiplier *= 1f + stagger;
            }
        }
    }

    /// <summary>
    /// <c>chain_lightning</c> (Thor's Chain): a hit has a 15% chance to arc to up to three other enemies within 8 m of the
    /// target, nearest first, each taking X% of the hit's damage (every combat type, after the critical roll and the
    /// outgoing bonuses, before the target's resistances) as lightning. Decided and dealt on the attacker's client:
    /// each arc is the player's own hit (<see cref="SecondaryHits"/>), routed by the game to that target's owner, and
    /// each struck enemy flashes with the game's networked chain-lightning hit effect, seen by every client nearby.
    /// </summary>
    internal static class ChainLightning
    {
        private const float Chance = 0.15f;
        private const float Radius = 8f;
        private const int MaxArcs = 3;

        private static readonly NetVisual Flash = new NetVisual("fx_chainlightning_hit", "fx_lightningweapon_hit");

        /// <summary>The damage of each arc when the chain fires on this hit, else 0.</summary>
        public static float Proc(float share, Player player, Character target, HitData hit)
        {
            if (share <= 0f || !SecondaryHits.IsFoe(player, target) || Random.value >= Chance)
            {
                return 0f;
            }
            return DamageSlots.Combat(in hit.m_damage) * share;
        }

        /// <summary>After the hit went out: the arcs from the struck target.</summary>
        public static void Arc(Character origin, Skills.SkillType skill, float damage)
        {
            Player? player = Player.m_localPlayer;
            if (player == null || origin == null)
            {
                return;
            }
            Vector3 from = origin.GetCenterPoint();
            foreach (Character target in SecondaryHits.Near(player, origin.transform.position, Radius, MaxArcs, origin))
            {
                HitData arc = SecondaryHits.NewHit(player, target, skill, from);
                arc.m_damage.m_lightning = damage;
                SecondaryHits.Deal(target, arc);
                Flash.Spawn(arc.m_point);
            }
        }
    }
}
