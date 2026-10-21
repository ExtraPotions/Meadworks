// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Companions/PackLeader.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; NativeHusbandry exception guard; factors by the portable YieldMath. With the
// Follow Command extension more tames follow a player, and each of them is a pack member.
// SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Shepherd.Yield;
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Pack leader: a tamed creature that follows a player (<c>MonsterAI.GetFollowTarget</c> is that player) deals more
    /// damage and takes less, from the followed player's Husbandry level (<see cref="HusbandrySkill.Of"/>).
    /// <list type="bullet">
    /// <item>Damage dealt: the creature's attack runs on its owner, where its AI and its follow target live, and calls
    /// <c>Character.Damage</c> on the victim there. A prefix multiplies the hit by 1 + the Pack Damage share before it
    /// goes; a postfix gives the caller its HitData back unchanged, so a reused hit object is never scaled twice.</item>
    /// <item>Damage taken: <c>Character.RPC_Damage</c> runs on the victim's owner, once per hit. A prefix multiplies it by
    /// 1 - the Pack Toughness share when the victim is such a follower.</item>
    /// </list>
    /// Summons are not animals and get neither. Nothing happens while Husbandry is off; a setting of 0 turns its half off.
    /// </summary>
    public static class PackLeader
    {
        [HarmonyPatch(typeof(Character), nameof(Character.Damage), typeof(HitData))]
        private static class Dealt
        {
            [HarmonyPrefix]
            private static void Prefix(HitData hit, out HitData.DamageTypes? __state)
            {
                __state = null;
                if (hit == null || !HusbandrySkill.Active || hit.m_attacker.IsNone())
                    return;
                float factor = NativeHusbandry.Run("pack damage", static h => DealtFactor(h), hit, 1f);
                if (factor <= 1f)
                    return;
                __state = hit.m_damage;
                hit.m_damage.Modify(factor);
            }

            [HarmonyPostfix]
            private static void Postfix(HitData hit, HitData.DamageTypes? __state)
            {
                if (__state.HasValue && hit != null)
                    hit.m_damage = __state.Value;
            }
        }

        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class Taken
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, HitData hit)
            {
                if (hit == null || !HusbandrySkill.Active || __instance.IsPlayer() || !__instance.IsTamed())
                    return;
                NativeHusbandry.Run("pack toughness", static taken => Soften(taken.character, taken.hit), (character: __instance, hit));
            }
        }

        /// <summary>The player a tamed animal follows, on the machine that runs its AI (its owner); null otherwise or for summons.</summary>
        public static Player? LeaderOf(Character creature)
        {
            if (creature == null || creature.IsPlayer() || !creature.IsTamed() || IsSummon(Herd.TameableOf(creature)))
                return null;
            MonsterAI? ai = creature.GetBaseAI() as MonsterAI;
            GameObject? target = ai != null ? ai.GetFollowTarget() : null;
            return target != null ? target.GetComponent<Player>() : null;
        }

        /// <summary>A summoned creature: it levels its owner's skill or disappears when left behind or logged out.</summary>
        public static bool IsSummon(Tameable tameable) =>
            tameable == null || tameable.m_levelUpOwnerSkill != global::Skills.SkillType.None
            || tameable.m_unsummonDistance > 0f || tameable.m_unsummonOnOwnerLogoutSeconds > 0f;

        private static float DealtFactor(HitData hit)
        {
            Player? leader = LeaderOf(hit.GetAttacker());
            if (leader == null)
                return 1f;
            return (float)YieldMath.PackDealt(HusbandrySkill.Share(HusbandryCompanionSettings.PackDamage.Value, HusbandrySkill.Of(leader)));
        }

        private static void Soften(Character creature, HitData hit)
        {
            ZNetView view = creature.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner())
                return;
            Player? leader = LeaderOf(creature);
            if (leader == null)
                return;
            float share = HusbandrySkill.Share(HusbandryCompanionSettings.PackToughness.Value, HusbandrySkill.Of(leader));
            if (share > 0f)
                hit.m_damage.Modify((float)YieldMath.PackTaken(share));
        }
    }
}
