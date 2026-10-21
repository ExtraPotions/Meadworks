// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using HarmonyLib;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// Kill attribution for Reaper and Soul Reaper (effect <c>on_kill_restore</c>).
    /// <list type="bullet">
    /// <item>The kill is seen on the dying creature's ZDO owner (Character.OnDeath returns early elsewhere, and only the
    /// owner records the last hit): often a nearby client on a dedicated server, not necessarily the killer.</item>
    /// <item>The killer is the last hit's attacker when that is a player (the same rule Loot uses for its find
    /// stats). The owner cannot see the killer's gear, so it does not compute anything: when the killer's player ZDO
    /// says it wants kill restores (<see cref="PlayerStats.WantsKillRestore"/>, published by the killer's own client), it
    /// sends one argument-less routed RPC to the killer's peer only (the owner of the killer's player ZDO). When the
    /// owner is the killer, the routed RPC is handled locally without a network send.</item>
    /// <item>The killer's client restores from its own aggregate totals, exactly as for its other stats.</item>
    /// </list>
    /// One small message per kill by a player with Reaper or Soul Reaper, to that player's peer. A kill by anyone else,
    /// or no player at all (a fall, a pet), sends nothing.
    /// </summary>
    internal static class KillCredit
    {
        private const string Rpc = "ECF_KillRestore";

        private static readonly System.Action<long> Handler = OnKillRestore;

        /// <summary>ZNet.Awake: register on the session's routed RPC table (a new one each session, every peer).</summary>
        public static void Register()
        {
            ZRoutedRpc.instance?.Register(Rpc, Handler);
        }

        /// <summary>On the creature's owner, as it dies.</summary>
        public static void OnCreatureDeath(Character creature)
        {
            if (creature.IsPlayer() || creature.m_nview == null || !creature.m_nview.IsValid() || !creature.m_nview.IsOwner())
            {
                return;
            }
            Player? killer = creature.m_lastHit?.GetAttacker() as Player;
            ZDO? zdo = killer != null && killer.m_nview != null ? killer.m_nview.GetZDO() : null;
            if (zdo == null || zdo.GetOwner() == 0L || ZRoutedRpc.instance == null
                || !PlayerStats.Wants(zdo, PlayerStats.WantsKillRestore))
            {
                return;
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(zdo.GetOwner(), Rpc);
        }

        // On the killer's own client.
        private static void OnKillRestore(long sender)
        {
            Player? player = Player.m_localPlayer;
            if (player == null || player.IsDead() || !ItemEffects.Enabled)
            {
                return;
            }
            float[] restore = AggregateHost.Current.KillRestore;
            if (restore[AggregateValues.Health] > 0f)
            {
                player.Heal(restore[AggregateValues.Health]);
            }
            if (restore[AggregateValues.Stamina] > 0f)
            {
                player.AddStamina(restore[AggregateValues.Stamina]);
            }
            if (restore[AggregateValues.Eitr] > 0f)
            {
                player.AddEitr(restore[AggregateValues.Eitr]);
            }
        }
    }

    [HarmonyPatch]
    internal static class KillCreditPatches
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static void RegisterRpc() => KillCredit.Register();

        // Prefix: the owner has the last hit before the death clears anything; Player.OnDeath is an override, so
        // creatures (Character/Humanoid) come through here and players never do.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Character), nameof(Character.OnDeath))]
        private static void CreatureDied(Character __instance) => KillCredit.OnCreatureDeath(__instance);
    }
}
