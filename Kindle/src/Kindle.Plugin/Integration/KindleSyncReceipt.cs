// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Kindle.Runtime
{
    // Jotunn's initial receipt precedes Crucible's authenticated epoch Hello. As in
    // NativeSessionAdapter, proof belongs to the exact native connection, not a
    // pre-Hello generation number. It can bind to that connection's first epoch
    // once; a different RPC/network/established epoch or world cannot reuse it.
    internal sealed class KindleSyncReceipt
    {
        ZNet network;
        ZRpc server;
        string epoch = "";
        long world;
        internal void Observe()
        {
            network = ZNet.instance;
            server = network != null && !network.IsServer() ? network.GetServerRPC() : null;
            epoch = ""; world = 0;
            if (server != null && server.IsConnected() && CruciblePlugin.IsReady)
            {
                var session = CruciblePlugin.Session;
                if (session.Role == SessionRole.RemoteClient && !string.IsNullOrEmpty(session.SessionId))
                { epoch = session.SessionId; world = session.WorldId; }
            }
        }
        internal long CurrentGeneration
        {
            get
            {
                if (!CruciblePlugin.IsReady || network == null || server == null || !server.IsConnected()
                    || !ReferenceEquals(network, ZNet.instance) || network.IsServer()
                    || !ReferenceEquals(server, network.GetServerRPC())) return -1;
                var session = CruciblePlugin.Session;
                if (session.Role != SessionRole.RemoteClient || !session.AuthorityReady
                    || session.WorldId == 0 || string.IsNullOrEmpty(session.SessionId)) return -1;
                if (epoch.Length == 0) { epoch = session.SessionId; world = session.WorldId; }
                return epoch == session.SessionId && world == session.WorldId ? session.Generation : -1;
            }
        }
    }
}
