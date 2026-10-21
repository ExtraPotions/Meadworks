using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;

namespace Burrow.Reach
{
    /// <summary>
    /// Epic Loot's enchanting table (enchant, augment, rune, convert, upgrade and the other tabs) pays its materials
    /// through its own inventory code, not the game's ConsumeResources, so the crafting patches never see it. Epic
    /// Loot publishes <c>EpicLoot.API.RegisterInventoryProvider</c> for exactly this: Burrow registers a count and a
    /// take for the reachable containers, found by reflection so nothing of Epic Loot is referenced. Epic Loot counts
    /// and removes from the player's inventory first and asks the provider only for the rest. No item list is given
    /// (that one feeds the tabs that pick gear to enchant or sacrifice), so gear in chests is never altered in place;
    /// only materials are paid from them. The <c>Crafting</c> switch decides, as for the crafting panel.
    /// </summary>
    public static class EpicLootLink
    {
        public const string Guid = "randyknapp.mods.epicloot";
        private const string ProviderId = "Burrow";

        /// <summary>Called once from the plugin's Start, after every plugin has loaded. Silent without Epic Loot.</summary>
        public static void Register()
        {
            MethodInfo register = RegisterMethod();
            if (register == null)
                return;
            try
            {
                object[] args = { ProviderId, null, (Func<string, int>)Count, (Func<string, int, int>)Take, null };
                if (register.Invoke(null, args) is bool ok && ok)
                    Plugin.Log.LogInfo("Burrow: Epic Loot's enchanting table pays materials from nearby containers");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Burrow: Epic Loot link failed, {e.GetType().Name}: {e.Message}");
            }
        }

        private static MethodInfo RegisterMethod()
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out PluginInfo info) || info.Instance == null)
                return null;
            Type api = info.Instance.GetType().Assembly.GetType("EpicLoot.API");
            MethodInfo method = api?.GetMethod("RegisterInventoryProvider", BindingFlags.Public | BindingFlags.Static);
            if (method == null)
                Plugin.Log.LogWarning("Burrow: this Epic Loot version has no RegisterInventoryProvider; its table pays from the inventory only");
            return method;
        }

        /// <summary>Items of a shared name in the reachable containers (Epic Loot adds the inventory itself).</summary>
        private static int Count(string name)
        {
            return ReachRules.Active(ReachMode.Crafting) ? ReachCount.InContainers(name, -1, true) : 0;
        }

        /// <summary>The part the inventory could not pay, taken from the reachable containers, nearest first.</summary>
        private static int Take(string name, int amount)
        {
            if (amount <= 0 || !ReachRules.Active(ReachMode.Crafting))
                return 0;
            return ReachPayment.TakeFromContainers(name, amount, -1, true);
        }
    }
}
