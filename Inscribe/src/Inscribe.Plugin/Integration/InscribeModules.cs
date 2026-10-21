// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, per-module step-aside, module switches) and the
// once-per-session notices (design sections 5, 6 and 10.1); same shape as Wildblood.Plugin.
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Inscribe.Config;
using ExtraPotions.Inscribe.Integration;
using ExtraPotions.Inscribe.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using StepAsideDecision = ExtraPotions.Inscribe.Integration.StepAsideDecision;
using StepAsideRules = ExtraPotions.Inscribe.Integration.ModuleStepAside;

namespace ExtraPotions.Inscribe.Runtime
{
    /// <summary>
    /// Inscribe turns off for the session on a vanilla server or a Crucible server without Inscribe (C02); items keep and
    /// show their state. Each module also stays off while its source mod runs. Step-aside is decided once after all
    /// plugins load.
    /// </summary>
    public static class InscribeModules
    {
        static StepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;
        const string TranslationProduct = InscribeCore.TokenNamespace;

        public static StepAsideDecision StepAside => stepAside ?? StepAsideRules.Decide(Enumerable.Empty<string>());

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, StepAsideRules.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            stepAside = StepAsideRules.Decide(Chainloader.PluginInfos.Keys);
            foreach (var notice in stepAside.Notices)
                log.Info("Inscribe " + notice.Key.Substring("step_aside_".Length) + " steps aside for " + notice.SourceNames + ".");
        }

        /// <summary>Inscribe as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        public static bool Active(InscribeModule module)
        {
            var s = Plugin.Settings;
            if (!Plugin.Ready || s == null || StepAside.SteppedAside(module) || !s.Get(InscribeSettings.General.Enabled)) return false;
            if (module == InscribeModule.Display) return MeadworksHeadless.AllowsUi;
            if (!ProductActive(out _)) return false;
            switch (module)
            {
                case InscribeModule.Rarity: return s.Get(InscribeSettings.General.RarityEnabled);
                // The enhancement conversion writes inscriptions, so it runs exactly while rarity does.
                case InscribeModule.Enhancement: return s.Get(InscribeSettings.General.RarityEnabled) && !StepAside.SteppedAside(InscribeModule.Rarity);
                case InscribeModule.Drops: return s.Get(InscribeSettings.General.RarityEnabled);
                case InscribeModule.Cheats: return true;
                default: return false;
            }
        }

        /// <summary>
        /// Called from Update once the local player exists: one step-aside notice and one session-off notice per session.
        /// Each stepped-aside module is reported to Crucible (C09 per-module step-aside: registry, log and HUD once per
        /// module and source); the per-session center message shows the same Crucible notice text from Inscribe's keys.
        /// </summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                InscribeText.Centre(string.Join("\n", stepAside.Notices.Select(n => InscribeText.Format(n.Key, n.SourceNames))), log);
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = InscribeText.Text(reason == ConfigBlockReason.VanillaServer ? InscribeTextKeys.OffNoCrucible : InscribeTextKeys.OffNoInscribe);
                InscribeText.Centre(text, log);
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
