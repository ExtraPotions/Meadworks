// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, step-aside per module with the sources' own switches, module
// switches) and the once-per-session notices (design sections 5, 6 and 11.1).
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Conversion;
using ExtraPotions.Wildblood.Integration;
using ExtraPotions.Wildblood.Localization;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using StepAsideDecision = ExtraPotions.Wildblood.Integration.StepAsideDecision;
// Crucible 064e74b adds ExtraPotions.Meadworks.Crucible.ModuleStepAside; Wildblood's own rules class keeps the name here.
using ModuleStepAside = ExtraPotions.Wildblood.Integration.ModuleStepAside;

namespace ExtraPotions.Wildblood.Runtime
{
    /// <summary>
    /// Wildblood turns off for the session on a vanilla server or a Crucible server without Wildblood (C02); Luck keeps
    /// running client-side there (user decision Q6). Each module also stays off while a source mod acts on it. Step-aside
    /// is decided after all plugins load and again when Defer To DropNSpawn changes.
    /// </summary>
    public static class WildbloodModules
    {
        static StepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;

        static SourceSwitches? decidedSwitches;
        static bool decidedDefer;

        /// <summary>The step-aside decision; Defer To DropNSpawn is rechecked at use, so a change (local or from the server) applies at once.</summary>
        public static StepAsideDecision StepAside
        {
            get
            {
                if (stepAside == null || decidedSwitches == null) return stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>(), SourceSwitches.Unknown, true);
                var defer = Plugin.Settings?.Get(WildbloodSettings.Loot.DeferToDropNSpawn) ?? true;
                if (defer != decidedDefer) { decidedDefer = defer; stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, decidedSwitches, defer); noticedSession = -1; }
                return stepAside;
            }
        }

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            var switches = SourceSwitches.Read(ReadConfig(SourceImport.FileName(ImportSource.EliteCreaturesRebornRules)),
                ReadConfig(SourceImport.FileName(ImportSource.CreatureManagerConfig)), ReadConfig(SourceImport.FileName(ImportSource.SaveYourLuckBruhConfig)), ReadConfig(SourceImport.FileName(ImportSource.EliteCreaturesRebornConfig)));
            var defer = Plugin.Settings?.Get(WildbloodSettings.Loot.DeferToDropNSpawn) ?? true;
            decidedSwitches = switches; decidedDefer = defer;
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, switches, defer);
            foreach (var notice in stepAside.Notices)
                log.Info("Wildblood " + notice.Module.ToString().ToLowerInvariant() + " module steps aside for " + notice.SourceNames + ".");
        }

        /// <summary>Wildblood as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        public static bool Active(WildbloodModule module)
        {
            var s = Plugin.Settings;
            if (!Plugin.Ready || s == null || StepAside.SteppedAside(module) || !s.Get(WildbloodSettings.General.Enabled)) return false;
            if (module == WildbloodModule.Luck) return s.Get(WildbloodSettings.General.LuckEnabled); // Q6: runs without Wildblood on the server
            if (!ProductActive(out _)) return false;
            switch (module)
            {
                case WildbloodModule.Stars: return s.Get(WildbloodSettings.General.StarsEnabled);
                case WildbloodModule.Traits: return s.Get(WildbloodSettings.General.TraitsEnabled);
                case WildbloodModule.Bosses: return s.Get(WildbloodSettings.General.BossesEnabled);
                case WildbloodModule.Loot: return s.Get(WildbloodSettings.General.LootEnabled);
                case WildbloodModule.Pressure: return s.Get(WildbloodSettings.General.PressureEnabled);
                case WildbloodModule.AI: return s.Get(WildbloodSettings.AI.Enabled);
                case WildbloodModule.Horde: return s.Get(WildbloodSettings.Horde.Enabled);
                case WildbloodModule.Display:
                case WildbloodModule.Recap: return MeadworksHeadless.AllowsUi;
                default: return false;
            }
        }

        /// <summary>Called from Update once the local player exists: one step-aside notice and one session-off notice per session.</summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.Any && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                WildbloodText.Centre(string.Join("\n", stepAside.Notices.Select(n => WildbloodText.Format(n.Key, n.SourceNames))), log);
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = WildbloodText.Text(reason == ConfigBlockReason.VanillaServer ? WildbloodTextKeys.OffNoCrucible : WildbloodTextKeys.OffNoWildblood);
                WildbloodText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            stepAside = null;
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
