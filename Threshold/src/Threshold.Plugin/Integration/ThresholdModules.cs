// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: product activity (peer-class gate, whole-product step-aside, Enabled) and the once-per-session
// notices (design sections 5 and 9.1). Same shape as Edge.Plugin's EdgeModules.
using System.Linq;
using BepInEx.Bootstrap;
using ExtraPotions.Meadworks.Crucible;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Integration;
using ExtraPotions.Threshold.Localization;
using ExtraPotions.Threshold.Picker;
using CruciblePlugin = ExtraPotions.Meadworks.Crucible.Plugin;
using StepAsideDecision = ExtraPotions.Threshold.Integration.StepAsideDecision;

namespace ExtraPotions.Threshold.Runtime
{
    /// <summary>
    /// Threshold turns off for the session on a vanilla server or a Crucible server without Threshold (C02), and stays off
    /// while a portal router it replaces is installed (C09). Step-aside is decided once after all plugins load.
    /// </summary>
    public static class ThresholdModules
    {
        static StepAsideDecision? stepAside;
        static long noticedSession = -1;
        static long offNoticedSession = -1;
        static bool teleportEverything;
        static long teNoticedSession = -1;

        public static bool SteppedAside => stepAside?.SteppedAside ?? false;

        /// <summary>After every plugin has loaded: declares the sources to Crucible (diagnostics, persisted log notice) and decides.</summary>
        public static void Decide(MeadworksLog log)
        {
            if (!CruciblePlugin.Services.StepAside.Declared(Plugin.Guid).Any())
                MeadworksStepAside.Declare(Plugin.Guid, Plugin.Name, ProductStepAside.Sources.Select(s => new SourceMod(s.Guid, s.Name)));
            MeadworksStepAside.ShouldYield(Plugin.Guid);
            stepAside = ProductStepAside.Decide(Chainloader.PluginInfos.Keys);
            if (stepAside.SteppedAside)
                log.Info("Threshold steps aside for " + stepAside.SourceNames + ".");
            teleportEverything = TeleportEverythingStepAside.Installed(Chainloader.PluginInfos.Keys);
            if (teleportEverything)
                log.Info("Threshold leaves Teleport Listed Items and Bring Tames to " + TeleportEverythingStepAside.Name + ".");
        }

        /// <summary>Threshold as a product may act in this session (peer class gate; true in single player and on the host).</summary>
        public static bool ProductActive(out ConfigBlockReason reason)
        {
            reason = ConfigBlockReason.None;
            return Plugin.Ready && MeadworksAuthority.ProductActive(Plugin.Guid, out reason);
        }

        /// <summary>Threshold may route portals now: registered, not stepped aside, Enabled and active for the session.</summary>
        public static bool Active()
        {
            var s = Plugin.Settings;
            return Plugin.Ready && s != null && !SteppedAside && s.Get(ThresholdSettings.General.Enabled) && ProductActive(out _);
        }

        /// <summary>Teleport Listed Items and Bring Tames may act: Threshold routes and TeleportEverything is absent (T19).</summary>
        public static bool ExtrasActive() => TeleportEverythingStepAside.ExtrasActive(teleportEverything, Active());

        /// <summary>The server-authoritative portal mode, independent of jump speed and sea gates.</summary>
        public static TeleportMode Mode => Plugin.Settings?.Get(ThresholdSettings.General.TeleportMode) ?? TeleportMode.Map;

        public static bool PortalRoutingActive() => Active() && PortalModeRules.Routes(Mode);

        public static bool ServerActive() => PortalRoutingActive();

        /// <summary>Portal discovery may widen the game's portal prefab list: registered, not stepped aside and Enabled.
        /// Runs on every machine, a dedicated server included, independent of the session's peer class.</summary>
        public static bool DiscoveryEnabled()
        {
            var s = Plugin.Settings;
            return Plugin.Ready && s != null && stepAside != null && !SteppedAside && s.Get(ThresholdSettings.General.Enabled) && PortalModeRules.Routes(Mode);
        }

        /// <summary>Called from Update once the local player exists: one step-aside notice and one session-off notice per session.</summary>
        public static void Tick(MeadworksLog log)
        {
            if (!Plugin.Ready || Player.m_localPlayer == null) return;
            var session = CruciblePlugin.Session;
            if (stepAside != null && stepAside.SteppedAside && noticedSession != session.Generation)
            {
                noticedSession = session.Generation;
                ThresholdText.Centre(ThresholdText.Format(ThresholdTextKeys.StepAside, stepAside.SourceNames), log);
            }
            var s = Plugin.Settings;
            if (teleportEverything && teNoticedSession != session.Generation && s != null && Active()
                && (s.Get(ThresholdSettings.Tames.BringTames) || s.Get(ThresholdSettings.Cheats.TeleportListedItems)))
            {
                teNoticedSession = session.Generation;
                ThresholdText.Centre(ThresholdText.Text(ThresholdTextKeys.TeleportEverythingStepAside), log);
            }
            if (offNoticedSession != session.Generation && session.AuthorityReady && !ProductActive(out var reason)
                && (reason == ConfigBlockReason.VanillaServer || reason == ConfigBlockReason.ProductDisabled))
            {
                offNoticedSession = session.Generation;
                var text = ThresholdText.Text(reason == ConfigBlockReason.VanillaServer ? ThresholdTextKeys.OffNoCrucible : ThresholdTextKeys.OffNoThreshold);
                ThresholdText.Centre(text, log);
                log.Info(text);
            }
        }

        public static void Reset()
        {
            stepAside = null;
            noticedSession = -1;
            offNoticedSession = -1;
            teleportEverything = false;
            teNoticedSession = -1;
        }
    }
}
