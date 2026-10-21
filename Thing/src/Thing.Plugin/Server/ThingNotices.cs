// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: server-to-player notices (design 3, 6 and 11). Thing is required on all peers, so the server sends a
// localization key and its arguments and each player's Thing shows the text in that player's language. Admin-written texts
// (reasons, broadcasts, message of the day) travel as written. Temporary transport: one routed RPC owned by Thing until a
// Crucible notice service exists (recorded as a Crucible item).
using System;
using System.Linq;
using ExtraPotions.Meadworks.Crucible;

namespace ExtraPotions.Thing.Runtime
{
    public static class ThingNotices
    {
        public const string RpcName = "ExtraPotions_Thing_Notice";
        const char Separator = '\u001f';

        static ZRoutedRpc? registered;
        static MeadworksLog? log;

        public static void Initialize(MeadworksLog logger) => log = logger;

        /// <summary>Registers the notice RPC once per routed RPC instance (a new one exists per session).</summary>
        public static void Tick()
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null || ReferenceEquals(rpc, registered)) return;
            rpc.Register<string, string>(RpcName, Receive);
            registered = rpc;
        }

        /// <summary>Sends a notice to one player (peer UID) or to everybody (<see cref="ZRoutedRpc.Everybody"/>). Key empty: raw text.</summary>
        public static void Send(long target, string key, params object[] arguments)
        {
            var rpc = ZRoutedRpc.instance;
            if (rpc == null) return;
            var joined = string.Join(Separator.ToString(), arguments.Select(a => Convert.ToString(a, System.Globalization.CultureInfo.InvariantCulture) ?? ""));
            rpc.InvokeRoutedRPC(target, RpcName, key ?? "", joined);
        }

        public static void Broadcast(string key, params object[] arguments) => Send(ZRoutedRpc.Everybody, key, arguments);

        static void Receive(long sender, string key, string joined)
        {
            var rpc = ZRoutedRpc.instance;
            var net = ZNet.instance;
            if (rpc == null || net == null) return;
            // Only the server speaks for Thing: a remote client accepts notices from the server peer, the server from itself.
            if (!net.IsServer() && sender != rpc.GetServerPeerID()) return;
            if (net.IsServer() && sender != rpc.m_id) return;
            var args = (joined ?? "").Split(Separator);
            var text = string.IsNullOrEmpty(key) ? joined ?? "" : ThingText.Format(key, args.Cast<object>().ToArray());
            if (log != null) ThingText.Centre(text, log);
        }

        public static void Shutdown()
        {
            registered = null;
        }
    }
}
