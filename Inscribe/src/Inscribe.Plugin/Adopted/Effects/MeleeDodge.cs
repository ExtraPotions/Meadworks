// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Evader's Fury's trigger: a melee attack that passes through the local player's dodge. The game decides a dodged
    /// hit on the attacker's side (Attack.DoMeleeAttack / DoAreaAttack skip a dodge-invincible target and call
    /// Player.HitWhileDodging, and never send the damage), so the dodging player's own client never sees such a hit in
    /// its damage RPC. The game's own "hit while dodging" RPC carries no hint of melee or ranged (projectiles send it
    /// too), so while a melee or area attack resolves on the attacker's peer, a dodged player with Evader's Fury (its
    /// player ZDO's <see cref="PlayerStats.WantsDodgeFury"/> bit, published by its own client) also gets one
    /// argument-less routed RPC (<c>ECF_MeleeDodged</c>) to the peer owning its player ZDO, where the window opens; a
    /// player without it gets nothing. Registered on every peer's routed RPC table at ZNet.Awake, like <see cref="KillCredit"/>.
    /// </summary>
    internal static class MeleeDodge
    {
        private const string Rpc = "ECF_MeleeDodged";

        private static readonly Action<long> Handler = OnDodged;

        /// <summary>A melee or area attack is resolving its hits on this peer (main thread).</summary>
        public static bool InMelee;

        public static void Register() => ZRoutedRpc.instance?.Register(Rpc, Handler);

        /// <summary>On the attacker's peer, as the game tells a dodging player about a hit it avoided.</summary>
        public static void OnHitWhileDodging(Player player)
        {
            ZDO? zdo = InMelee && player.m_nview != null ? player.m_nview.GetZDO() : null;
            if (zdo != null && zdo.GetOwner() != 0L && ZRoutedRpc.instance != null
                && PlayerStats.Wants(zdo, PlayerStats.WantsDodgeFury))
            {
                ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), Rpc);
            }
        }

        // On the dodging player's own client.
        private static void OnDodged(long sender)
        {
            Player? player = Player.m_localPlayer;
            if (player != null && !player.IsDead())
            {
                CombatWindows.OnDodgedMelee();
            }
        }
    }

    [HarmonyPatch]
    internal static class MeleeDodgePatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static void RegisterRpc() => MeleeDodge.Register();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
        private static void MeleeBegin() => MeleeDodge.InMelee = true;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Attack), nameof(Attack.DoMeleeAttack))]
        private static void MeleeEnd() => MeleeDodge.InMelee = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Attack), nameof(Attack.DoAreaAttack))]
        private static void AreaBegin() => MeleeDodge.InMelee = true;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Attack), nameof(Attack.DoAreaAttack))]
        private static void AreaEnd() => MeleeDodge.InMelee = false;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.HitWhileDodging))]
        private static void Dodged(Player __instance) => MeleeDodge.OnHitWhileDodging(__instance);
    }
}
