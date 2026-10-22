// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using HarmonyLib;

namespace ExtraPotions.Barter.Runtime
{
    [HarmonyPatch(typeof(ZDO), "Deserialize")]
    internal static class ZDO_Deserialize_BarterTrader
    {
        private static void Postfix(ZDO __instance) => TraderAuthority.Received(__instance);
    }

    [HarmonyPatch(typeof(ZNetScene), "RPC_SpawnObject")]
    internal static class ZNetScene_RPC_SpawnObject_BarterTrader
    {
        private static bool Prefix(long spawner, int prefabHash) => TraderAuthority.CanSpawn(spawner, prefabHash);
    }

    [HarmonyPatch(typeof(ZNet), "ServerLoadWorld")]
    internal static class ZNet_ServerLoadWorld_BarterTraders
    {
        private static void Postfix() => TraderAuthority.CaptureWorld();
    }

    [HarmonyPatch(typeof(ZNetView), "Awake")]
    internal static class ZNetView_Awake_BarterTrader
    {
        private static void Prefix(out bool __state) => __state = ZNetView.m_initZDO == null;
        private static void Postfix(ZNetView __instance, bool __state)
        {
            // An RPC-created ZDO enters Awake through m_initZDO and must never be registered as a server spawn.
            if (__state && __instance.IsValid()) TraderAuthority.ServerCreated(__instance.GetZDO());
        }
    }

    [HarmonyPatch(typeof(ZDO), "SetOwner")]
    internal static class ZDO_SetOwner_BarterTrader
    {
        private static bool Prefix(ZDO __instance, long __0) => ZNet.instance == null || !ZNet.instance.IsServer() ||
            !TraderAuthority.Owns(__instance) || __0 == ZDOMan.GetSessionID();
    }

    [HarmonyPatch(typeof(ZDO), "SetOwnerInternal")]
    internal static class ZDO_SetOwnerInternal_BarterTrader
    {
        private static bool Prefix(ZDO __instance, long __0) => ZNet.instance == null || !ZNet.instance.IsServer() ||
            !TraderAuthority.Owns(__instance) || __0 == ZDOMan.GetSessionID();
    }
}
