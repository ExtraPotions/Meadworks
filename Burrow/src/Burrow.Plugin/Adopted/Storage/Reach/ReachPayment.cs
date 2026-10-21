using System;
using System.Collections.Generic;
using HarmonyLib;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>
    /// Paying from containers. The game pays a recipe, an upgrade or a piece through
    /// <c>Inventory.RemoveItem(name, amount, quality, worldLevel)</c> on the player's inventory, either from
    /// <c>Player.ConsumeResources</c> or, for single ingredient recipes, straight from <c>InventoryGui.DoCrafting</c>.
    /// Both callers open a payment window; inside it a prefix on that <c>RemoveItem</c> takes the shortfall
    /// (amount minus what the inventory holds) from the reachable containers, nearest first, and then lets the
    /// game remove what the inventory has. The inventory therefore always pays first with the game's own code.
    /// Nothing can be lost: the game calls RemoveItem only after the crafted item or the piece exists, the
    /// containers give up exactly the shortfall, and the inventory part is the game's untouched path. If a
    /// container vanished since counting the shortfall is paid short and logged; the player gained, not lost.
    /// </summary>
    public static class ReachPayment
    {
        private static int depth;
        private static ReachMode mode;

        public static bool Open => depth > 0;

        public static void Begin(ReachMode paymentMode)
        {
            if (depth == 0)
                mode = paymentMode;
            depth++;
        }

        public static void End()
        {
            if (depth > 0)
                depth--;
        }

        /// <summary>The RemoveItem prefix: takes from storage what the local player's inventory cannot pay.</summary>
        public static void PayShortfall(Inventory inventory, string name, int amount, int quality, bool worldLevel)
        {
            Player player = Player.m_localPlayer;
            if (!Open || player == null || inventory != player.GetInventory() || !ReachRules.Active(mode))
                return;
            int shortfall = amount - inventory.CountItems(name, quality, worldLevel);
            if (shortfall <= 0)
                return;
            int taken = TakeFromContainers(name, shortfall, quality, worldLevel);
            if (taken > 0)
                ReachFlash.Mark(name);
            if (taken < shortfall)
                Plugin.Log.LogWarning($"Burrow: {shortfall - taken} of {name} could not be taken from storage while paying; the containers changed since counting");
        }

        /// <summary>Removes up to amount items of a shared name from the reachable containers, nearest first. Returns what was removed.</summary>
        public static int TakeFromContainers(string name, int amount, int quality, bool worldLevel)
        {
            int remaining = amount;
            foreach (Container container in ReachCount.Containers())
            {
                if (remaining <= 0)
                    break;
                if (ReachCount.CountIn(container, name, quality, worldLevel) <= 0 || !ContainerScan.Claim(container))
                    continue;
                int taken = Remove(container, name, remaining, quality, worldLevel);
                if (taken > 0)
                    ContainerScan.Save(container);
                remaining -= taken;
            }
            return amount - remaining;
        }

        /// <summary>The game's RemoveItem loop, stack by stack, with the prefab's allow / deny lists applied per stack.</summary>
        private static int Remove(Container container, string name, int amount, int quality, bool worldLevel)
        {
            ContainerRule rule = ReachRules.RuleFor(container);
            Inventory inventory = container.GetInventory();
            int removed = 0;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (removed >= amount)
                    break;
                if (!ReachCount.Matches(item, name, quality, worldLevel) || !rule.Accepts(item))
                    continue;
                int take = Math.Min(item.m_stack, amount - removed);
                if (inventory.RemoveItem(item, take))
                    removed += take;
            }
            return removed;
        }
    }

    /// <summary>Opens the payment window around the game's consume path (quality 0 = piece, 1 = craft, more = upgrade).</summary>
    [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources))]
    public static class ConsumeResourcesWindow
    {
        [HarmonyPrefix]
        public static void Prefix(int qualityLevel) => ReachPayment.Begin(ReachRules.FromQuality(qualityLevel));

        [HarmonyFinalizer]
        public static void Finalizer() => ReachPayment.End();
    }

    /// <summary>Opens the payment window around crafting, which pays single ingredient recipes without ConsumeResources.</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    public static class CraftingWindow
    {
        [HarmonyPrefix]
        public static void Prefix(InventoryGui __instance)
        {
            ReachPayment.Begin(__instance.m_craftUpgradeItem == null ? ReachMode.Crafting : ReachMode.Upgrading);
        }

        [HarmonyFinalizer]
        public static void Finalizer() => ReachPayment.End();
    }

    /// <summary>Inside a payment window the shortfall comes from storage before the game removes the inventory part.</summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.RemoveItem), new[] { typeof(string), typeof(int), typeof(int), typeof(bool) })]
    public static class InventoryRemovePatch
    {
        [HarmonyPrefix]
        public static void Prefix(Inventory __instance, string name, int amount, int itemQuality, bool worldLevelBased)
        {
            if (ReachPayment.Open)
                ReachPayment.PayShortfall(__instance, name, amount, itemQuality, worldLevelBased);
        }
    }
}
