// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the Multi-User Chests switch and its step-aside for MultiUserChest (behavior inspired by
// MultiUserChest by MSchmoecker, MIT; no code reused).
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Burrow.Core;
using ExtraPotions.Burrow;
using ExtraPotions.Burrow.Embedded.SyncedConfig;
using HarmonyLib;

namespace Burrow.Integration
{
    /// <summary>
    /// The synced <c>Multi-User Chests</c> switch (section 0, default on). On, every player may use a chest another
    /// player has open: the effective Shared Chests mode is <c>Full</c>, so every change goes to the chest's owning
    /// client, which grants only what the chest holds when the request arrives. Off, the Shared Chests setting decides.
    /// While MultiUserChest is loaded the effective mode is <c>Off</c> and Burrow leaves chest access to it.
    /// </summary>
    public static class MultiUserChestYield
    {
        public static ConfigEntry<bool> Enabled { get; private set; }
        private static bool decided;
        private static StowRowSource source;

        public static void Bind(SyncedConfiguration synced)
        {
            Enabled = synced.Bind(CoreSettings.Section, "Multi-User Chests", true,
                "Several players can use the same chest at once. A chest another player has open opens for you too, and every change you make "
                + "is applied by the client that owns the chest, so nothing is duplicated or lost (the same as Shared Chests = Full). "
                + "Off: the Shared Chests setting decides. Ignored while MultiUserChest is installed. "
                + "A container another mod marks with the MUC_Ignore flag is never shared.");
        }

        /// <summary>The Shared Chests mode every Burrow module applies.</summary>
        public static SharedMode Mode => ModeOf(false);

        /// <summary>The mode for one container: <c>Off</c> while it carries the <c>MUC_Ignore</c> flag, else <see cref="Mode"/>.</summary>
        public static SharedMode ModeFor(Container container) => ModeOf(IsIgnored(container));

        /// <summary>Another mod set the ZDO bool <c>MUC_Ignore</c> on the container's ZNetView.</summary>
        public static bool IsIgnored(Container container)
        {
            ZNetView view = container != null ? container.m_nview : null;
            return view != null && view.IsValid() && view.GetZDO().GetBool(MultiUserChests.IgnoreKey);
        }

        private static SharedMode ModeOf(bool ignored)
        {
            SharedMode configured = CoreSettings.SharedChests.Value;
            switch (MultiUserChests.Resolve(Enabled == null || Enabled.Value, SteppedAside, ignored))
            {
                case MultiUserOutcome.SteppedAside:
                case MultiUserOutcome.Ignored: return SharedMode.Off;
                case MultiUserOutcome.AllUsers: return SharedMode.Full;
                default: return configured;
            }
        }

        /// <summary>
        /// True while MultiUserChest is loaded. Cached once a world is up (every plugin has loaded by then); asked
        /// earlier, the answer is computed again each time.
        /// </summary>
        public static bool SteppedAside
        {
            get
            {
                if (decided)
                    return source != null;
                source = Chainloader.PluginInfos != null ? MultiUserChests.StepAsideMatch(Chainloader.PluginInfos.Keys) : null;
                decided = ZNet.instance != null;
                return source != null;
            }
        }

        public static void Shutdown()
        {
            decided = false;
            source = null;
            Enabled = null;
        }

        /// <summary>Reports the step-aside once, when the inventory panel first exists.</summary>
        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
        private static class ReportPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                if (SteppedAside)
                    StowRowYield.ReportModule(source, MultiUserChests.ModuleId, MultiUserChests.TextKey);
            }
        }
    }
}
