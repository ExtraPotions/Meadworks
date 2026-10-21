// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (server-only peer class gate, step-aside per module with the source tools' own
// switches) and the step-aside reports (design sections 6 and 9); same model as Keel and Edge.
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Conversion;
using ExtraPotions.Delve.Engine;
using ExtraPotions.Delve.Integration;
using ExtraPotions.Delve.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Delve.Integration.ModuleStepAside;

namespace ExtraPotions.Delve.Runtime
{
    /// <summary>
    /// Delve acts only on the authority (C02 <c>ServerOnly</c>: single player, host, dedicated server) and idles on a
    /// remote client. Each module also stays off while its source tool acts on it (design 9). Step-aside is decided once
    /// after all plugins load and reported through Crucible C09 (log on a headless server, HUD on a graphical host).
    /// </summary>
    public static class DelveModules
    {
        static ModuleStepAsideDecision? stepAside;
        static bool reported;
        static long idleNoticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>(), SourceSwitches.Unknown);

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            var switches = SourceSwitches.Read(ReadConfig(SourceImport.FileName(ImportSource.ValheimAdminTool)),
                ReadConfig(SourceImport.FileName(ImportSource.EliteCreaturesReborn)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, switches);
            foreach (var notice in stepAside.Notices)
                log.Info("Delve " + notice.Module.ToString().ToLowerInvariant() + " module steps aside: " + notice.SourceName + " is installed.");
        }

        /// <summary>Delve may act in this session: true on the authority, false on a remote client (reason ServerOnly).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        /// <summary>A module may act now: product active, General / Enabled on, not stepped aside.</summary>
        public static bool Active(DelveModule module)
        {
            var s = Plugin.Settings;
            if (s == null || !ProductActive(out _) || StepAside.SteppedAside(module)) return false;
            if (module == DelveModule.Rooms && !s.Get(DelveSettings.Rooms.Enabled)) return false;
            return s.Get(DelveSettings.General.Enabled);
        }

        /// <summary>A kind may act now: its module is active and its own switch is on.</summary>
        public static bool Active(DelveKind kind)
        {
            var s = Plugin.Settings;
            return s != null && Active(ModuleStepAside.ModuleOf(kind)) && s.Get(DelveSettings.Kind(kind).Enabled);
        }

        /// <summary>Called from Update: one step-aside report per module through C09, one idle line per remote session.</summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready) return;
            if (!reported && stepAside != null)
            {
                reported = true;
                foreach (var n in stepAside.Notices)
                    MeadworksStepAside.ReportModule(Plugin.Guid, n.Module.ToString().ToLowerInvariant(), new SourceMod(n.SourceGuid, n.SourceName), DelveCore.TokenNamespace, n.Key);
            }
            var session = CruciblePlugin.Session;
            if (idleNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason) && reason == ConfigBlockReason.ServerOnly)
            {
                idleNoticedSession = session.Generation;
                log.Debug(DelveText.Text(DelveTextKeys.ServerOnlyIdle));
            }
        }

        public static void Reset()
        {
            stepAside = null;
            reported = false;
            idleNoticedSession = -1;
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
