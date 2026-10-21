// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; reuses Ember/Kindle's exact native-connection receipt before the first authenticated Hello.
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Beacon.Runtime
{
    internal sealed class BeaconSyncReceipt
    {
        ZNet network;
        ZRpc server;
        string epoch = "";
        long world;
        internal void Reset() { network = null; server = null; epoch = ""; world = 0; }
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
