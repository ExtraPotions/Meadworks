// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using HarmonyLib;

namespace ExtraPotions.Meadworks.Crucible {
    // Native RPC_RoutedRPC ignores its actual ZRpc argument and trusts m_senderPeerID from the payload.
    // Keep the actual connection around synchronous dispatch; only this CS4 guard consumes it.
    [HarmonyPatch(typeof(ZRoutedRpc),"RPC_RoutedRPC")]
    internal static class WorldSettingRoutedContext {
        internal static ZRpc? Current;
        static void Prefix(ZRpc __0,out ZRpc? __state){__state=Current;Current=__0;}
        static Exception? Finalizer(Exception? __exception,ZRpc? __state){Current=__state;return __exception;}
        internal static void Reset()=>Current=null;
    }
    [HarmonyPatch(typeof(ZRoutedRpc),"InvokeRoutedRPC",new[]{typeof(long),typeof(ZDOID),typeof(string),typeof(object[])})]
    internal static class WorldSettingNativeDispatch {
        internal static int Depth;
        // A native invocation may run while servicing an incoming admin RPC (including its GlobalKeys publication).
        // Distinguish that fresh server-created data from the incoming packet being relayed; payload IDs cannot do this.
        static void Prefix(out int __state){__state=Depth;Depth++;}
        static Exception? Finalizer(Exception? __exception,int __state){Depth=__state;return __exception;}
    }
    [HarmonyPatch(typeof(ZRoutedRpc),"RouteRPC")]
    internal static class WorldSettingRelayGuard {
        static readonly int GlobalKeys="GlobalKeys".GetStableHashCode();
        // Clients never author a complete snapshot. Inspect the game's already-parsed header, without copying payloads.
        static bool Prefix(ZRoutedRpc.RoutedRPCData __0)=>WorldSettingNativeDispatch.Depth>0||WorldSettingRoutedContext.Current==null||ZNet.instance==null
            ||!ZNet.instance.IsServer()||__0.m_methodHash!=GlobalKeys;
    }
    [HarmonyPatch(typeof(ZoneSystem),"RPC_SetGlobalKey")]
    internal static class WorldSettingSetGuard {
        static bool Prefix(long __0,string __1)=>MeadworksWorldSettings.AllowMutation(__1,false,__0);
    }
    [HarmonyPatch(typeof(ZoneSystem),"RPC_RemoveGlobalKey")]
    internal static class WorldSettingRemoveGuard {
        static bool Prefix(long __0,string __1)=>MeadworksWorldSettings.AllowMutation(__1,true,__0);
    }
    [HarmonyPatch(typeof(ZoneSystem),"RPC_GlobalKeys")]
    internal static class WorldSettingReceiveGuard {
        static bool Prefix(long __0){
            var net=ZNet.instance;
            var peer=net?.GetServerPeer();
            return net!=null&&!net.IsServer()&&ReferenceEquals(WorldSettingRoutedContext.Current,net.GetServerRPC())
                &&peer!=null&&peer.m_uid==__0;
        }
    }
}
