// Adapted from assets/references/source/ValheimMods-openkeep-3.0.0/GrindstoneSkills/GrindstoneSkills/Defense/Core/BlockHooks.cs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Copyright (C) 2026 Gerald Glasgow. SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Saga namespace, configuration, CS1 and ownership boundaries.
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Saga.Runtime.Adopted.Grindstone
{
    /// <summary>
    /// The local player's block of the hit in <see cref="IncomingHit"/>. Humanoid.BlockAttack runs inside
    /// Character.RPC_Damage when the hit is blockable and the player is blocking (or Reflex made them); it returns false
    /// for a hit from behind, and true when the blocker took the hit, even when the hit broke the guard. Read from the
    /// game code 2026-09-27:
    /// <list type="bullet">
    /// <item>It parries when the blocker has a timed block bonus and the block was raised less than 0.25 s ago
    /// (<c>m_blockTimer</c>, a constant in the code): the prefix widens that window (<see cref="ParryWindow"/>) and
    /// works out whether this block parries.</item>
    /// <item>It spends stamina through SEMan.ModifyBlockStaminaUsage (<see cref="StaminaPerks"/>) and gains adrenaline
    /// through Player.AddAdrenaline (<see cref="AdrenalinePerk"/>), both while <see cref="InBlock"/>.</item>
    /// <item>It wears the blocker (<c>m_durability</c>) and shrinks the hit's push force; the postfix works on both.</item>
    /// </list>
    /// </summary>
    public static class BlockHooks
    {
        /// <summary>The local player's BlockAttack is running.</summary>
        public static bool InBlock { get; private set; }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.BlockAttack))]
        private static class Block
        {
            [HarmonyPrefix]
            private static void Prefix(Humanoid __instance, HitData hit, out BlockState __state)
            {
                __state = null;
                if (!IncomingHit.Active || !DefenseSkill.IsLocal(__instance) || hit == null)
                    return;
                __state = HookGuard.Run("defense block", static block => BlockState.Before(block.humanoid, block.hit), (humanoid: __instance, hit), (BlockState)null);
                InBlock = __state != null;
            }

            [HarmonyPostfix]
            private static void Postfix(Humanoid __instance, HitData hit, Character attacker, bool __result, BlockState __state)
            {
                if (__state != null && __result)
                    HookGuard.Run("defense blocked", static block => Blocked((Player)block.humanoid, block.hit, block.attacker, block.state),
                        (humanoid: __instance, hit, attacker, state: __state));
            }

            [HarmonyFinalizer]
            private static void Finalizer(Humanoid __instance, BlockState __state)
            {
                if (__state == null)
                    return;
                __state.Restore(__instance);
                InBlock = false;
            }
        }

        private static void Blocked(Player player, HitData hit, Character attacker, BlockState state)
        {
            IncomingHit.Blocked = true;
            IncomingHit.Parried = state.Parry;
            IncomingHit.WithShield = state.Shield;
            IncomingHit.GuardHeld = GuardHeld(player);
            float stopped = Mathf.Max(0f, state.BlockableBefore - hit.GetTotalBlockableDamage());
            state.RefundWear();
            StandFirm.OnBlock(hit);
            if (!IncomingHit.GuardHeld)
                return;
            Reflex.OnBlocked(player);
            if (state.Parry)
                Riposte.OnParry();
            else if (state.Shield)
                ShieldBash.OnBlock(attacker, hit);
            Thorns.OnBlock(player, attacker, hit, stopped);
        }

        /// <summary>
        /// The block held: the blocker still has stamina, and the block's stagger damage did not stagger them. The game
        /// sets the stagger damage to exactly the threshold when it staggers (Character.AddStaggerDamage); the
        /// animator's stagger state only shows a frame later.
        /// </summary>
        private static bool GuardHeld(Player player) =>
            player.HaveStamina() && player.m_staggerDamage < player.GetStaggerTreshold();
    }
}
