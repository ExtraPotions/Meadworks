// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Core/IncomingHit.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The hit reaching the local player right now. Character.RPC_Damage runs on the owner of the character hit, so
    /// for a player on that player's own client, where Defense lives; it applies difficulty, blocks
    /// (Humanoid.BlockAttack, <see cref="BlockHooks"/>), armour, and then takes the health off (Character.ApplyDamage,
    /// <see cref="DamageIntake"/>). A prefix opens this scope with the hit as it arrived; the block and damage hooks
    /// write what happened into it; the finalizer hands the outcome to experience, Hardened and the combat clock, then
    /// closes it. A dodged hit leaves the method early and so earns and adds nothing.
    /// </summary>
    public static class IncomingHit
    {
        public static bool Active { get; private set; }
        public static HitData Hit { get; private set; }
        public static Character Attacker { get; private set; }

        /// <summary>The hit's total damage as it arrived, before difficulty, blocking and armour.</summary>
        public static float RawDamage { get; private set; }

        /// <summary>The hit came from a creature, or from another player while PvP hits train: it trains Defense.</summary>
        public static bool Trains { get; private set; }

        public static bool Blocked { get; set; }
        public static bool Parried { get; set; }
        public static bool WithShield { get; set; }

        /// <summary>The block held: the blocker kept stamina and was not staggered by it.</summary>
        public static bool GuardHeld { get; set; }

        /// <summary>The damage taken off the player's health by this hit.</summary>
        public static float Taken { get; set; }

        [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
        private static class Arrive
        {
            [HarmonyPrefix]
            private static void Prefix(Character __instance, HitData hit, out bool __state)
            {
                __state = !Active && hit != null && DefenseSkill.Active && DefenseSkill.IsLocal(__instance);
                if (__state)
                    HookGuard.Run("defense hit", static arrive => Begin((Player)arrive.character, arrive.hit), (character: __instance, hit));
            }

            [HarmonyFinalizer]
            private static void Finalizer(Character __instance, bool __state)
            {
                if (!__state)
                    return;
                HookGuard.Run("defense hit outcome", static character => Resolve((Player)character), __instance);
                End((Player)__instance);
            }
        }

        private static void Begin(Player player, HitData hit)
        {
            Active = true;
            Hit = hit;
            Attacker = hit.GetAttacker();
            RawDamage = hit.GetTotalDamage();
            Trains = TrainsDefense(hit, Attacker, player);
            Reflex.Before(player, hit, Attacker);
        }

        private static bool TrainsDefense(HitData hit, Character attacker, Player player)
        {
            if (attacker == null || attacker == player)
                return false;
            if (hit.m_hitType == HitData.HitType.EnemyHit)
                return !attacker.IsPlayer();
            return hit.m_hitType == HitData.HitType.PlayerHit && attacker.IsPlayer() && DefenseExperienceSettings.PlayerHitsTrain.Value;
        }

        private static void Resolve(Player player)
        {
            if (Attacker != null)
                Recovery.MarkCombat();
            if (!Trains)
                return;
            DefenseXp.OnHit(player);
            if (!Blocked && Taken > 0f)
                Hardened.AddStack();
        }

        private static void End(Player player)
        {
            Reflex.After(player);
            Active = false;
            Hit = null;
            Attacker = null;
            RawDamage = Taken = 0f;
            Trains = Blocked = Parried = WithShield = GuardHeld = false;
        }
    }
}
