// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC03b): the four places Beacon watches in the game's join flow. Applied only while the join module
// runs (never when AutoConnector is installed, never on a headless process).
using System;
using ExtraPotions.Beacon.Joining;
using HarmonyLib;

namespace ExtraPotions.Beacon.Runtime
{
    [HarmonyPatch]
    internal static class BeaconJoinPatches
    {
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.JoinServer)), HarmonyPrefix]
        static void JoinServer(FejdStartup __instance) => Guard(() => BeaconJoin.OnJoinServer(__instance.m_joinServer));

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_ClientHandshake)), HarmonyPrefix]
        static bool Handshake(ZNet __instance, ZRpc rpc, bool needPassword, string serverPasswordSalt)
        {
            try
            {
                if (BeaconJoin.DeferHandshake(__instance, rpc, needPassword, serverPasswordSalt)) return false;
                BeaconJoin.BeforeHandshake(needPassword);
            }
            catch (Exception error) { Plugin.Log?.Warning("Beacon join hook: " + error.GetType().Name); }
            return true;
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.RPC_ClientHandshake)), HarmonyFinalizer]
        static Exception? HandshakeDone(Exception? __exception)
        {
            Guard(BeaconJoin.AfterHandshake);
            return __exception;
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnPasswordEntered)), HarmonyPrefix]
        static void PasswordEntered(string pwd) => Guard(() => BeaconJoin.BeforePasswordEntered(pwd));

        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.ShowConnectError)), HarmonyPostfix]
        static void ConnectError(ZNet.ConnectionStatus statusOverride) => Guard(() =>
        {
            var status = statusOverride == ZNet.ConnectionStatus.None ? ZNet.GetConnectionStatus() : statusOverride;
            BeaconJoin.OnMenu(ConnectionOutcomes.Parse(status.ToString()));
        });

        /// <summary>A Beacon failure never breaks the game's join; only the exception type is logged, never its text.</summary>
        static void Guard(Action action)
        {
            try { action(); }
            catch (Exception error) { Plugin.Log?.Warning("Beacon join hook: " + error.GetType().Name); }
        }
    }
}
