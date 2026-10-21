// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: session facts for the Core rules, per-module step-aside with the sources' own switches, and the
// once-per-session notices (design sections 3, 5 and 9.1; user decision Q1).
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Conversion;
using ExtraPotions.Cairn.Integration;
using ExtraPotions.Cairn.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using StepAsideDecision = ExtraPotions.Cairn.Integration.StepAsideDecision;
using ModuleStepAside = ExtraPotions.Cairn.Integration.ModuleStepAside;
using SessionFacts = ExtraPotions.Cairn.Config.SessionFacts;

namespace ExtraPotions.Cairn.Runtime
{
    public static class CairnModules
    {
        static StepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long heldNoticedSession = -1;

        public static StepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>(), SourceSwitches.Unknown);

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            var switches = SourceSwitches.Read(ReadConfig(SourceImport.FileName(ImportSource.TimeWeatherHudConfig)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, switches);
            foreach (var notice in stepAside.Notices)
                log.Info("Cairn " + notice.Module.ToString().ToLowerInvariant() + " steps aside for " + notice.SourceNames + ".");
        }

        /// <summary>The live facts the Core rules decide on (Crucible session, server presence, Cairn on the server).</summary>
        public static SessionFacts Facts()
        {
            if (!CruciblePlugin.IsReady) return new SessionFacts(SessionKind.RemoteClient, ServerKind.Unknown, false);
            var session = CruciblePlugin.Session;
            switch (session.Role)
            {
                case SessionRole.Host: return new SessionFacts(SessionKind.Host, ServerKind.Cairn, true);
                case SessionRole.DedicatedServer: return new SessionFacts(SessionKind.DedicatedServer, ServerKind.Cairn, true);
                case SessionRole.RemoteClient: return new SessionFacts(SessionKind.RemoteClient, Server(), session.IsAdmin);
                default: return SessionFacts.SinglePlayer;
            }
        }

        private static ServerKind Server()
        {
            switch (MeadworksAuthority.Server)
            {
                case ServerCrucible.Absent: return ServerKind.Vanilla;
                case ServerCrucible.Present:
                    switch (MeadworksAuthority.ServerProductOf(Plugin.Guid))
                    {
                        case ServerProduct.Present: return ServerKind.Cairn;
                        case ServerProduct.Missing: return ServerKind.CrucibleWithoutCairn;
                        default: return ServerKind.Unknown;
                    }
                default: return ServerKind.Unknown;
            }
        }

        /// <summary>
        /// Called from Update once the local player exists: each stepped-aside module is reported to Crucible (C09 per-module
        /// step-aside, once per module and source, from Cairn's keys), and the Q1 hold is announced once per session.
        /// </summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                foreach (var notice in stepAside.Notices)
                    foreach (var source in stepAside.SourcesOf(notice.Module))
                        MeadworksStepAside.ReportModule(Plugin.Guid, notice.Module.ToString().ToLowerInvariant(), new SourceMod(source.Guid, source.Name),
                            CairnCore.TokenNamespace, notice.Key);
            }
            var facts = Facts();
            if (heldNoticedSession != session.Generation && session.AuthorityReady && GatePolicy.Holds(facts) && facts.Server != ServerKind.Unknown)
            {
                heldNoticedSession = session.Generation;
                var text = CairnText.Text(CairnTextKeys.HeldNotice);
                CairnText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            stepAside = null;
            noticedSession = -1;
            heldNoticedSession = -1;
        }

        private static string? ReadConfig(string fileName)
        {
            try
            {
                var path = Path.Combine(Paths.ConfigPath, fileName);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
