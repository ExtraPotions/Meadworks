// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: per-module activity (peer class gate, step-aside per module with the sources' own switches, module
// switches) and the once-per-session notices (design sections 5, 6 and 10.1).
using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Bootstrap;
using ExtraPotions.Edge.Config;
using ExtraPotions.Edge.Conversion;
using ExtraPotions.Edge.Integration;
using ExtraPotions.Edge.Localization;
using ExtraPotions.Meadworks.Crucible;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using StepAsideDecision = ExtraPotions.Edge.Integration.StepAsideDecision;
using ModuleStepAside = ExtraPotions.Edge.Integration.ModuleStepAside;

namespace ExtraPotions.Edge.Runtime
{
    /// <summary>
    /// Edge turns off for the session on a vanilla server or a Crucible server without Edge (C02, user decision Q1: slope
    /// assist and swim re-equip too). Each module also stays off while a source mod acts on it. Step-aside is decided once
    /// after all plugins load.
    /// </summary>
    public static class EdgeModules
    {
        static StepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;

        public static StepAsideDecision StepAside => stepAside ?? ModuleStepAside.Decide(Enumerable.Empty<string>(), SourceSwitches.Unknown);

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics) and decides per module.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ModuleStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            var switches = SourceSwitches.Read(ReadConfig(SourceImport.FileName(ImportSource.SecondaryAttacksConfig)),
                ReadConfig(SourceImport.FileName(ImportSource.KeepWeaponsAfterSwimConfig)));
            stepAside = ModuleStepAside.Decide(Chainloader.PluginInfos.Keys, switches);
            foreach (var notice in stepAside.Notices)
                log.Info("Edge " + notice.Module.ToString().ToLowerInvariant() + " steps aside for " + notice.SourceNames + ".");
        }

        /// <summary>Edge as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        public static bool Active(EdgeModule module)
        {
            var s = Plugin.Settings;
            if (!Plugin.Ready || s == null || StepAside.SteppedAside(module) || !s.Get(EdgeSettings.General.Enabled) || !ProductActive(out _)) return false;
            switch (module)
            {
                case EdgeModule.Secondaries: return s.Get(EdgeSettings.General.SecondariesEnabled);
                case EdgeModule.Quickstep: return s.Get(EdgeSettings.General.QuickstepEnabled);
                case EdgeModule.Slope: return s.Get(EdgeSettings.General.SlopeAssistEnabled);
                case EdgeModule.SwimReequip: return s.Get(EdgeSettings.General.EquipmentEnabled);
                case EdgeModule.LastShield: return s.Get(EdgeSettings.General.EquipmentEnabled) && s.Get(EdgeSettings.Equipment.AutoEquipLastShield);
                case EdgeModule.Archery: return true;
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
                EdgeText.Centre(string.Join("\n", stepAside.Notices.Select(n => EdgeText.Format(n.Key, n.SourceNames))), log);
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = EdgeText.Text(reason == ConfigBlockReason.VanillaServer ? EdgeTextKeys.OffNoCrucible : EdgeTextKeys.OffNoEdge);
                EdgeText.Centre(text, log);
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
