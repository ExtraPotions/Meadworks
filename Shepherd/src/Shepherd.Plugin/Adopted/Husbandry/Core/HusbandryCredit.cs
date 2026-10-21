// Adapted from ValheimMods GrindstoneSkills/GrindstoneSkills/Husbandry/Core/HusbandryCredit.cs
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: namespace; Shepherd RPC name; registered whenever the Husbandry patches install as well as
// in ZNet.Awake (Shepherd patches late), never twice on one ZRoutedRpc; the handler does nothing while Husbandry is off.
// SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;
using UnityEngine;

namespace ExtraPotions.Shepherd.Runtime.Adopted.Husbandry
{
    /// <summary>
    /// Husbandry experience earned on another machine: a creature's owner knows who killed it, but only the killer's
    /// client can raise the killer's skill. The owner sends a routed RPC (<see cref="RpcName"/>) to that player's machine
    /// alone (<see cref="PlayerIds.PeerOf"/>, Everybody when it cannot tell); only the client whose local player has that
    /// ID acts on it. An offline player loses it. A hive's owner sends one Honey credit per honey a harvest hands out.
    /// </summary>
    public static class HusbandryCredit
    {
        public const string RpcName = "shepherd.husbandry_credit";

        public enum Kind
        {
            Butchering = 1,
            Honey = 2,
        }

        private static ZRoutedRpc? registeredOn;

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
        private static class NetAwake
        {
            [HarmonyPostfix]
            private static void Postfix() => NativeHusbandry.Run("husbandry credit", Register);
        }

        /// <summary>Registers the handler on the current ZRoutedRpc once; the game makes a fresh one each session.</summary>
        public static void Register()
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, registeredOn))
                return;
            registeredOn = rpc;
            if (!rpc.m_functions.ContainsKey(RpcName.GetStableHashCode()))
                rpc.Register<long, int, string>(RpcName, Receive);
        }

        /// <summary>Sends a credit of <paramref name="kind"/> for <paramref name="creaturePrefab"/> to the player with this ID.</summary>
        public static void Send(long playerId, Kind kind, string creaturePrefab)
        {
            if (playerId == 0L || ZRoutedRpc.instance == null)
                return;
            ZRoutedRpc.instance.InvokeRoutedRPC(PlayerIds.PeerOf(playerId), RpcName, playerId, (int)kind, creaturePrefab ?? "");
        }

        private static void Receive(long sender, long playerId, int kind, string creaturePrefab) =>
            NativeHusbandry.Run("husbandry credit", () => Credit(playerId, (Kind)kind, creaturePrefab));

        private static void Credit(long playerId, Kind kind, string creaturePrefab)
        {
            Player player = Player.m_localPlayer;
            if (!HusbandrySkill.Active || player == null || player.GetPlayerID() != playerId)
                return;
            if (kind == Kind.Butchering)
                HusbandryXp.RaiseForCreature(player, HusbandryExperienceSettings.Butchering.Value, creaturePrefab);
            else if (kind == Kind.Honey)
                HusbandryXp.Raise(player, Mathf.Max(0f, HusbandryExperienceSettings.Honey.Value));
        }
    }
}
