// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, step-aside per module with GrindstoneSkills' own Sailing
// switch) and the session notices (design sections 5 and 9.1); same model as Edge and Ember.
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Conversion;
using ExtraPotions.Keel.Integration;
using ExtraPotions.Keel.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Keel.Integration.ModuleStepAside;

namespace ExtraPotions.Keel.Runtime
{
    /// <summary>
    /// Keel turns off for the session on a vanilla server or a Crucible server without Keel (C02, design 5); each module
    /// also stays off while its source mod acts on it (design 9.1). Step-aside is decided once after all plugins load.
    /// </summary>
    public static class KeelModules
    {
        static ModuleStepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>(), SourceSwitches.Unknown);

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            var switches = SourceSwitches.Read(ReadConfig(SourceImport.FileName(ImportSource.GrindstoneSkills)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, switches);
            foreach (var notice in stepAside.Notices)
                log.Info("Keel " + notice.Module.ToString().ToLowerInvariant() + " module steps aside: " + notice.SourceName + " is installed.");
        }

        /// <summary>Keel as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && KeelAuthority.ProductActive(out reason);
        }

        /// <summary>A module may act now: product active, not stepped aside, and its own switch on.</summary>
        public static bool Active(KeelModule module)
        {
            var s = Plugin.Settings;
            if (s == null || !ProductActive(out _) || StepAside.SteppedAside(module)) return false;
            return module != KeelModule.Sailing || s.Get(KeelSettings.Sailing.Enabled);
        }

        /// <summary>
        /// Called from Update once the local player exists: step-aside reports go to the Crucible C09 registry (Almanac
        /// diagnostics, with Keel's keys) without a center message; <see cref="Decide"/> already logged them. Also shows
        /// one session-off notice per session.
        /// </summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                foreach (var n in stepAside.Notices)
                    CruciblePlugin.Services.StepAside.ReportModule(new ExtraPotions.Meadworks.Crucible.ModuleStepAside(
                        Plugin.Guid, n.Module.ToString().ToLowerInvariant(), new SourceMod(n.SourceGuid, n.SourceName), n.Key));
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = KeelText.Text(reason == ConfigBlockReason.VanillaServer ? KeelTextKeys.OffNoCrucible : KeelTextKeys.OffNoKeel);
                KeelText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            stepAside = null;
            KeelAuthority.Reset();
            noticedSession = -1;
            offNoticedSession = -1;
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