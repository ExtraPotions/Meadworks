// Shepherd - ExtraPotions Meadworks animal husbandry.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExtraPotions.Shepherd.Breeding;
using ExtraPotions.Meadworks.Crucible;
using Jotunn.Managers;
using UnityEngine;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;

namespace ExtraPotions.Shepherd.Runtime.Integration
{
    /// <summary>
    /// Host reads the optional facts capability and shares one line. A client uses that line only after this world's
    /// config sync, never its own Solstice or a previous world's saved line.
    /// </summary>
    internal static class ShepherdSeasons
    {
        static ConfigEntry<string>? facts;
        static SeasonSyncReceipt receipt = new SeasonSyncReceipt();
        static SeasonBreedRate reading;
        static float nextRead;
        static long generation = -1;
        static bool bound;

        internal static SeasonBreedRate Current => reading;
        internal static SeasonBreedRate Breeding(bool enabled) => enabled && reading.Active ? reading : default;

        internal static void Initialize(ConfigFile file)
        {
            if (bound) return;
            facts = MeadworksConfig.BindHidden(file, "Seasons", "Effective Facts", "",
                "Managed by Shepherd: optional season facts from the host. Do not edit.", ConfigScope.ServerAuthoritative);
            SynchronizationManager.OnConfigurationSynchronized += Synchronized;
            bound = true;
        }

        static void Synchronized(object sender, Jotunn.Utils.ConfigurationSynchronizationEventArgs args)
        {
            if (Names(args.UpdatedPluginGUIDs)) receipt.Observe();
            nextRead = 0f;
        }

        internal static void Tick()
        {
            if (!CruciblePlugin.IsReady || ZNet.instance == null || !CruciblePlugin.Session.AuthorityReady
                || CruciblePlugin.Session.WorldId == 0 || string.IsNullOrEmpty(CruciblePlugin.Session.SessionId))
            {
                reading = default;
                generation = -1;
                nextRead = 0f;
                return;
            }
            var session = CruciblePlugin.Session;
            if (generation != session.Generation)
            {
                reading = default;
                generation = session.Generation;
                nextRead = 0f;
            }
            if (Time.unscaledTime < nextRead) return;
            nextRead = Time.unscaledTime + 0.25f;
            if (session.IsAuthority && ZNet.instance.IsServer())
            {
                var text = "";
                try
                {
                    if (CruciblePlugin.Services.Capabilities.TryResolve<Func<string>>(SeasonBreeding.Id, SeasonBreeding.Version, out var provider) && provider != null)
                        text = provider() ?? "";
                }
                catch (Exception) { text = ""; }
                if (!SeasonBreeding.TryParse(text, out reading)) { text = ""; reading = default; }
                var carried = text.Length == 0 ? "" : SeasonBreeding.Wrap(session.WorldId, session.SessionId, text);
                if (facts != null && facts.Value != carried)
                    MeadworksConfig.Amend(facts, Plugin.Guid, ConfigScope.ServerAuthoritative, carried);
            }
            else
            {
                var text = receipt.CurrentGeneration == session.Generation && facts != null
                    ? MeadworksConfig.Effective(facts, Plugin.Guid, ConfigScope.ServerAuthoritative, "") : "";
                if (!SeasonBreeding.TryUnwrap(text, session.WorldId, session.SessionId, out var body) || !SeasonBreeding.TryParse(body, out reading))
                    reading = default;
            }
        }

        internal static void Shutdown()
        {
            if (bound) SynchronizationManager.OnConfigurationSynchronized -= Synchronized;
            bound = false;
            facts = null;
            receipt = new SeasonSyncReceipt();
            reading = default;
            generation = -1;
            nextRead = 0f;
        }

        static bool Names(IEnumerable<string>? ids)
        {
            if (ids == null) return false;
            foreach (var id in ids)
                if (id == Plugin.Guid) return true;
            return false;
        }
    }

    /// <summary>The sync receipt belongs to one connection. A later epoch or world needs its own sync.</summary>
    internal sealed class SeasonSyncReceipt
    {
        ZNet? network;
        ZRpc? server;
        string epoch = "";
        long world;

        internal void Observe()
        {
            network = ZNet.instance;
            server = network != null && !network.IsServer() ? network.GetServerRPC() : null;
            epoch = "";
            world = 0;
            if (server != null && server.IsConnected() && CruciblePlugin.IsReady)
            {
                var session = CruciblePlugin.Session;
                if (session.Role == SessionRole.RemoteClient && !string.IsNullOrEmpty(session.SessionId))
                {
                    epoch = session.SessionId;
                    world = session.WorldId;
                }
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
