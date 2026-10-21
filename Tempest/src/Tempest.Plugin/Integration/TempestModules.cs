// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, step-aside per module) and the session notices (design 6 and 9);
// same model as Keel and Ember.
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Tempest.Integration;
using ExtraPotions.Tempest.Localization;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Tempest.Integration.ModuleStepAside;

namespace ExtraPotions.Tempest.Runtime
{
    /// <summary>
    /// Tempest turns off for the session on a vanilla server or a Crucible server without Tempest (C02, design 6); each
    /// module also stays off while its source mod acts on it (design 9). Step-aside is decided once after all plugins load.
    /// </summary>
    public static class TempestModules
    {
        static ModuleStepAsideDecision? stepAside;
        static long offNoticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<InstalledPlugin>());

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Values.Select(p => new InstalledPlugin(p.Metadata.GUID, p.Metadata.Name)));
            if (!stepAside.Any) return;
            // No center message per module: each module goes into Crucible's step-aside registry (listed by the diagnostics
            // command) and the log gets one summary line.
            foreach (var n in stepAside.Notices)
                CruciblePlugin.Services.StepAside.ReportModule(new ExtraPotions.Meadworks.Crucible.ModuleStepAside(Plugin.Guid,
                    n.Module.ToString().ToLowerInvariant(), new SourceMod(n.SourceGuid, n.SourceName), n.Key));
            log.Info(string.Join(" ", stepAside.Notices.Select(n => TempestText.Format(n.Key, n.SourceName))));
        }

        /// <summary>Tempest as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        /// <summary>A module may act now: product active and not stepped aside.</summary>
        public static bool Active(TempestModule module) => Plugin.Settings != null && ProductActive(out _) && !StepAside.SteppedAside(module);

        /// <summary>Called from Update once the local player exists: one session-off notice per session.</summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = TempestText.Text(reason == ConfigBlockReason.VanillaServer ? TempestTextKeys.OffNoCrucible : TempestTextKeys.OffNoTempest);
                TempestText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            if (stepAside != null)
                foreach (var n in stepAside.Notices)
                    CruciblePlugin.Services.StepAside.WithdrawModule(Plugin.Guid, n.Module.ToString().ToLowerInvariant());
            stepAside = null;
            offNoticedSession = -1;
        }
    }
}
