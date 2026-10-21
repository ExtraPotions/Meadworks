// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using ExtraPotions.Inscribe.Adopted.Effects.Combat3;
using ExtraPotions.Inscribe.Adopted.Loot;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    // One patch per hot damage method (valheim-performance rule 12): each does the shared tests once (owner, local
    // player, the attacker's ZDO) and then asks each feature in turn. The critical roll (Priority.High) and the weapon's
    // on-hit effects (Priority.Low) keep their own Character.Damage prefixes: their priorities place them before and
    // after other mods' changes to the hit.

    /// <summary>
    /// Character.RPC_Damage, where the target's owner resolves a hit: the attacker context for Lingering Wounds and
    /// penetration (<see cref="HitOwnerContext"/>, every owner, a dedicated server too), then for the local player
    /// Forsaken Ward and the incoming effects (<see cref="IncomingHits"/>, which may avoid the hit), else the ally-hit
    /// flag on a creature (<see cref="AllyHits"/>). The attacker's ZDO is looked up once, on the owner only.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.RPC_Damage))]
    internal static class IncomingDamageDispatch
    {
        private static bool Prefix(Character __instance, HitData hit, out HitOwnerContext.Saved __state)
        {
            ZNetView nview = __instance.m_nview;
            bool owner = hit != null && nview != null && nview.IsValid() && nview.IsOwner();
            ZDO? attacker = owner && !hit!.m_attacker.IsNone() && ZDOMan.instance != null ? ZDOMan.instance.GetZDO(hit.m_attacker) : null;
            __state = HitOwnerContext.Enter(attacker);
            if (!owner)
            {
                return true;
            }
            if (__instance is Player player && ReferenceEquals(player, Player.m_localPlayer))
            {
                FireAndBossHits.ReduceBossHit(hit!);
                return IncomingHits.OnIncoming(player, hit!);
            }
            if (!__instance.IsPlayer())
            {
                AllyHits.Mark(nview!, hit!, attacker);
            }
            return true;
        }

        private static void Postfix(HitOwnerContext.Saved __state)
        {
            HitOwnerContext.Exit(__state);
            IncomingHits.Steadfast = false;
        }

        private static Exception? Finalizer(Exception? __exception)
        {
            if (__exception != null)
            {
                HitOwnerContext.Reset();
                IncomingHits.Steadfast = false;
            }
            return __exception;
        }
    }

    /// <summary>
    /// Character.ApplyDamage on the local player, after armor: Ember Skin lowers fire first, then the Runic Ward
    /// absorbs, and after it Bramblehide returns a share of the health actually lost (before minus after).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
    internal static class HealthLossDispatch
    {
        private static void Prefix(Character __instance, HitData hit, out float __state)
        {
            __state = -1f;
            if (!ReferenceEquals(__instance, Player.m_localPlayer))
            {
                return;
            }
            FireAndBossHits.ReduceBurning(hit);
            if (!__instance.IsDead())
            {
                IncomingHits.BeforeHealthLoss(hit);
                __state = __instance.GetHealth();
            }
        }

        private static void Postfix(Character __instance, HitData hit, float __state)
        {
            if (__state > 0f && __instance is Player player)
            {
                IncomingHits.AfterHealthLoss(player, hit, __state - player.GetHealth());
            }
        }
    }

    /// <summary>
    /// Character.Damage, where a hit is built (the attacker's peer): the local player's outgoing bonuses
    /// (<see cref="OutgoingHits"/>, one ZDOID compare), else a tagged summon's raised damage (<see cref="Summons"/>).
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class OutgoingDamageDispatch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            Player? player = Player.m_localPlayer;
            if (player != null && hit.m_attacker == player.GetZDOID())
            {
                if (ItemEffects.Enabled)
                {
                    OutgoingHits.OnHit(player, __instance, hit);
                }
                return;
            }
            Summons.OnHit(hit);
        }
    }
}
