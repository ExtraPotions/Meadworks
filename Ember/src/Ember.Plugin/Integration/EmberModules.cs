// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, step-aside per module, module switches) and the
// session notices (design sections 5 and 10.2).
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Integration;
using ExtraPotions.Ember.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using ModuleStepAside = ExtraPotions.Ember.Integration.ModuleStepAside;

namespace ExtraPotions.Ember.Runtime
{
    /// <summary>
    /// Ember turns off for the session on a vanilla server or a Crucible server without Ember (C02, user decision Q2);
    /// each module also stays off when its source mod is installed (Q3). Step-aside is decided once after all plugins load.
    /// </summary>
    public static class EmberModules
    {
        /// <summary>Ember's C05 catalog id; its step_aside_&lt;module&gt; keys are the Crucible module notice keys.</summary>
        const string TranslationProduct = "ember";

        static ModuleStepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;

        public static ModuleStepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>());

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys);
            foreach (var notice in stepAside.Notices)
                log.Info("Ember " + notice.Module.ToString().ToLowerInvariant() + " module steps aside: " + notice.SourceName + " is installed.");
        }

        /// <summary>Ember as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        public static bool Active(EmberModule module)
        {
            if (!ProductActive(out _) || StepAside.SteppedAside(module) || Plugin.Settings == null) return false;
            var s = Plugin.Settings;
            switch (module)
            {
                case EmberModule.Sleep: return s.Get(EmberSettings.Sleep.Enabled) && !s.Get(EmberSettings.Sleep.UseVanillaSleepWindow);
                case EmberModule.Torches: return s.Get(EmberSettings.Torches.Enabled);
                case EmberModule.Doors: return MeadworksHeadless.AllowsUi && s.Get(EmberSettings.Doors.AutoDoorsAllowed);
                default: return false;
            }
        }

        /// <summary>
        /// Called from Update once the local player exists: step-aside reports and one session-off notice per session.
        /// Each stepped-aside module is reported to Crucible (C09 per-module step-aside: registry, log and center HUD once
        /// per module and source, from Ember's keys). Ember shows no center message of its own for step-aside, so the
        /// first session no longer shows the same text twice; <see cref="Decide"/> still logs each module at start.
        /// </summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                foreach (var n in stepAside.Notices)
                {
                    var source = new SourceMod(stepAside.Present.First(p => p.Module == n.Module).Guid, n.SourceName);
                    var module = n.Module.ToString().ToLowerInvariant();
                    MeadworksStepAside.ReportModule(Plugin.Guid, module, source, TranslationProduct, n.Key);
                }
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = EmberText.Text(reason == ConfigBlockReason.VanillaServer ? EmberTextKeys.OffNoCrucible : EmberTextKeys.OffNoEmber);
                EmberText.Centre(text, log);
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
