// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module step-aside (design section 9, C09); same model as Thing, Tempest and Keel.
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Beacon.Integration;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Beacon.Integration.ModuleStepAside;

namespace ExtraPotions.Beacon.Runtime
{
    /// <summary>
    /// The join module steps aside while AutoConnector is loaded: Beacon then patches nothing in the join flow and says so
    /// once; its panel and converter stay available. Decided once after every plugin has loaded.
    /// </summary>
    public static class BeaconModules
    {
        const string TranslationProduct = BeaconCore.TokenNamespace;

        static ModuleStepAsideDecision? stepAside;
        static long noticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<InstalledPlugin>());

        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Values.Select(p => new InstalledPlugin(p.Metadata.GUID, p.Metadata.Name)));
            foreach (var notice in stepAside.Notices)
                log.Info("Beacon " + notice.Module.ToString().ToLowerInvariant() + " module steps aside: " + notice.SourceName + " is installed.");
        }

        /// <summary>A module may act now (Beacon is client-optional: no server gate on joining).</summary>
        public static bool Active(BeaconModule module) => Plugin.Ready && !StepAside.SteppedAside(module);

        /// <summary>Reports each step-aside through Crucible C09 once per session (registry, log and one notice from Beacon's keys).</summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || stepAside == null || !stepAside.Any) return;
            var session = CruciblePlugin.Session;
            if (noticedSession == session.Generation) return;
            noticedSession = session.Generation;
            foreach (var n in stepAside.Notices)
                MeadworksStepAside.ReportModule(Plugin.Guid, n.Module.ToString().ToLowerInvariant(), new SourceMod(n.SourceGuid, n.SourceName), TranslationProduct, n.Key);
        }

        public static void Reset()
        {
            stepAside = null;
            noticedSession = -1;
        }
    }
}
