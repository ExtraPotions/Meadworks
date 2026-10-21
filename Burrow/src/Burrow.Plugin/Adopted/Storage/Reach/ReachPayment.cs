using System;
using System.Collections.Generic;
using HarmonyLib;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>Funds native actions before they create output; the game consumes the resulting local materials.</summary>
    public static class ReachPayment
    {
        public static bool PrepareCraft(InventoryGui gui, Player player)
        {
            if (player == null || player != Player.m_localPlayer || gui.m_craftRecipe == null)
                return true;
            int quality = gui.m_craftUpgradeItem == null ? 1 : gui.m_craftUpgradeItem.m_quality + 1;
            if (!ReachRules.Active(ReachRules.FromQuality(quality)) || player.NoCostCheat()
                || ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost))
                return true;
            if (Resources.NativeInventoryGuards.Held(player.GetInventory()))
                return false;
            Recipe recipe = gui.m_craftRecipe;
            int multiplier = gui.m_multiCrafting ? gui.m_multiCraftAmount : 1;
            if (!player.HaveRequirements(recipe, false, quality, multiplier))
                return false;
            if (recipe.m_requireOnlyOneIngredient)
            {
                recipe.GetAmount(quality, out int need, out ItemDrop.ItemData single, multiplier);
                return single != null && Pull(player.GetInventory(), single.m_shared.m_name, need, single.m_quality);
            }
            return Prepare(player, recipe.m_resources, quality, multiplier);
        }

        public static bool PreparePiece(Player player, Piece piece)
        {
            if (player == null || player != Player.m_localPlayer || piece == null || !ReachRules.Active(ReachMode.Building)
                || player.m_noPlacementCost || ZoneSystem.instance.GetGlobalKey(piece.FreeBuildKey()))
                return true;
            return !Resources.NativeInventoryGuards.Held(player.GetInventory())
                && player.HaveRequirements(piece, Player.RequirementMode.CanBuild)
                && Prepare(player, piece.m_resources, 0, 1);
        }

        private static bool Prepare(Player player, Piece.Requirement[] requirements, int quality, int multiplier)
        {
            var costs = new Dictionary<string, int>();
            CraftingStation station = player.GetCurrentCraftingStation();
            foreach (Piece.Requirement requirement in requirements)
            {
                if (Requirements.Skipped(station, requirement))
                    continue;
                int need = checked(requirement.GetAmount(quality) * multiplier);
                if (need <= 0)
                    continue;
                string name = Requirements.Name(requirement);
                costs.TryGetValue(name, out int previous);
                costs[name] = checked(previous + need);
            }
            foreach (KeyValuePair<string, int> cost in costs)
                if (!Pull(player.GetInventory(), cost.Key, cost.Value, -1))
                    return false;
            return true;
        }

        private static bool Pull(Inventory inventory, string name, int amount, int quality)
        {
            int shortfall = amount - inventory.CountItems(name, quality, true);
            if (shortfall <= 0)
                return true;
            int moved = ReachPull.Move(inventory, item => ReachCount.Matches(item, name, quality, true), shortfall);
            if (moved > 0)
                ReachFlash.Mark(name);
            // A refused/short transfer leaves any moved materials in the player, as with explicit Pull.
            return inventory.CountItems(name, quality, true) >= amount;
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

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    public static class CraftingWindow
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(InventoryGui __instance, Player player, bool __runOriginal)
            => __runOriginal && ReachPayment.PrepareCraft(__instance, player);
    }

    [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
    public static class PlacementWindow
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Player __instance, Piece piece, bool __runOriginal)
            => __runOriginal && ReachPayment.PreparePiece(__instance, piece);
    }
}
