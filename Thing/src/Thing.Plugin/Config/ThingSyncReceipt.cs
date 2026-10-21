// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original; same receipt as Ember.Plugin's EmberSyncReceipt (expDARE).
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Thing.Runtime
{
    /// <summary>
    /// Jotunn's configuration receipt arrives before Crucible's authenticated session Hello. The receipt belongs to the exact
    /// native connection: it binds to that connection's first session once, and a different connection, session or world
    /// cannot reuse it, so locked values from an earlier server never apply to a new one.
    /// </summary>
    internal sealed class ThingSyncReceipt
    {
        ZNet? network;
        ZRpc? server;
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

        /// <summary>The session generation the receipt belongs to; -1 when no receipt from this connection is in force.</summary>
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
