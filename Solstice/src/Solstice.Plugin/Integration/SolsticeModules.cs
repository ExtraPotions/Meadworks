// Solstice - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: module activity (peer class gate, step-aside) and the session notices; same model as Tempest and Keel.
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Solstice.Integration;
using ExtraPotions.Solstice.Localization;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Solstice.Integration.ModuleStepAside;

namespace ExtraPotions.Solstice.Runtime
{
    /// <summary>
    /// Solstice turns off for the session on a vanilla server or a Crucible server without Solstice; it also stays off while another
    /// seasons mod (shudnal's Seasons, Seasonality) is installed. Step-aside is decided once after all plugins load.
    /// </summary>
    public static class SolsticeModules
    {
        /// <summary>Solstice's catalog id; its step_aside_&lt;module&gt; keys are the Crucible module notice keys.</summary>
        const string TranslationProduct = SolsticeCore.TokenNamespace;

        static ModuleStepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<InstalledPlugin>());

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Values.Select(p => new InstalledPlugin(p.Metadata.GUID, p.Metadata.Name)));
            foreach (var notice in stepAside.Notices)
                log.Info("Solstice " + notice.Module.ToString().ToLowerInvariant() + " module steps aside: " + notice.SourceName + " is installed.");
        }

        /// <summary>Solstice as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        /// <summary>A module may act now: product active and not stepped aside.</summary>
        public static bool Active(SolsticeModule module) => Plugin.Settings != null && ProductActive(out _) && !StepAside.SteppedAside(module);

        /// <summary>
        /// Called from Update once the local player exists: step-aside reports through Crucible (registry, log and one centre message per
        /// source, from Solstice's keys) and one session-off notice per session.
        /// </summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                foreach (var n in stepAside.Notices)
                    MeadworksStepAside.ReportModule(Plugin.Guid, n.Module.ToString().ToLowerInvariant(), new SourceMod(n.SourceGuid, n.SourceName), TranslationProduct, n.Key);
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = SolsticeText.Text(reason == ConfigBlockReason.VanillaServer ? SolsticeTextKeys.OffNoCrucible : SolsticeTextKeys.OffNoSolstice);
                SolsticeText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            stepAside = null;
            noticedSession = -1;
            offNoticedSession = -1;
        }
    }
}
