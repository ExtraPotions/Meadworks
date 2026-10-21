// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Core/DamageIntake.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The damage that reaches the local player's health. Character.ApplyDamage takes it off, after armour and blocking
    /// (from RPC_Damage) or directly (burning and poison ticks); it runs on the character's owner, so for the local
    /// player on their own client. Read from the game code 2026-09-27: for a player it first applies the world's
    /// damage-taken modifier (Game.m_localDamgeTakenRate), then subtracts the total from health.
    /// <list type="bullet">
    /// <item>The prefix scales the hit by every reduction Defense has (<see cref="Reductions"/>), then lets Last Stand
    /// keep a killing blow from killing (<see cref="LastStand"/>). While Last Stand's invulnerability lasts, the damage
    /// is skipped altogether.</item>
    /// <item>The postfix records what was taken, for <see cref="IncomingHit"/> and the combat clock.</item>
    /// </list>
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    public static class DamageIntake
    {
        [HarmonyPrefix]
        private static bool Prefix(Character __instance, HitData hit, out float __state)
        {
            __state = -1f;
            if (hit == null || !DefenseSkill.Active || !DefenseSkill.IsLocal(__instance) || __instance.IsDead())
                return true;
            Player player = (Player)__instance;
            __state = player.GetHealth();
            return HookGuard.Run("defense damage", static intake => Take(intake.player, intake.hit), (player, hit), true);
        }

        [HarmonyPostfix]
        private static void Postfix(Character __instance, float __state)
        {
            if (__state < 0f)
                return;
            float taken = __state - __instance.GetHealth();
            if (taken <= 0f)
                return;
            Recovery.MarkCombat();
            if (IncomingHit.Active)
                IncomingHit.Taken += taken;
        }

        /// <summary>False when the damage is to be skipped.</summary>
        private static bool Take(Player player, HitData hit)
        {
            if (LastStand.Invulnerable)
                return false;
            Scale(hit, Reductions.Keep(player));
            LastStand.Check(player, hit);
            return true;
        }

        /// <summary>Scales every damage type of the hit, the generic and non-player damage included (HitData.ApplyModifier leaves those two out).</summary>
        public static void Scale(HitData hit, float factor)
        {
            if (factor >= 1f)
                return;
            factor = Mathf.Max(0f, factor);
            hit.ApplyModifier(factor);
            hit.m_damage.m_damage *= factor;
            hit.m_damage.m_nonPlayer *= factor;
        }

        /// <summary>The health this hit will take once ApplyDamage applies the world's damage-taken modifier.</summary>
        public static float Final(HitData hit)
        {
            float untouched = hit.m_damage.m_damage + hit.m_damage.m_nonPlayer;
            return untouched + (hit.GetTotalDamage() - untouched) * Game.m_localDamgeTakenRate;
        }
    }
}
