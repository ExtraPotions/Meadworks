using System;
using System.Collections.Generic;
using ExtraPotions.Mantle.Embedded.EliteCraftingLink;
using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;

namespace Mantle.Elite
{
    /// <summary>
    /// A magic backpack stays magic when it is upgraded: each pack's recipe takes the pack before it, and the game crafts
    /// a brand-new item, so the old pack's EliteCrafting inscriptions (its custom data, every key starting <c>ecf_</c>)
    /// would be lost with it. EliteCrafting carries its keys over only for the game's own quality upgrade
    /// (<c>m_craftUpgradeItem</c>), which this is not. So, around the game's craft of one of Mantle's backpacks
    /// (<c>InventoryGui.DoCrafting</c>, the local player's crafting panel): the inventory's items are noted before; after
    /// it, when exactly one magic pack left the inventory (the cost) and exactly one new pack arrived, the old pack's
    /// <c>ecf_</c> keys are written onto the new one, as EliteCrafting's own carry-over does (a new dictionary, so its item
    /// cache reads the item afresh; the inscriptions keep their rolled values and tiers). An in-place upgrade
    /// (<see cref="Crafting.InPlaceUpgrade"/>) makes the game take the worn pack first, so a worn magic pack is the one
    /// carried over; otherwise the game takes the first copy it finds. In a postfix, before the in-place upgrade's
    /// finalizer can put back or drop an old pack the game did not take. Multi-crafting and quality upgrades are left
    /// alone; Epic Loot's data is not carried. The crafting client's own inventory, saved with the character.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    public static class MagicCarryOver
    {
        private static readonly HashSet<ItemDrop.ItemData> before = new HashSet<ItemDrop.ItemData>();

        /// <summary>The shared name of the backpack being crafted, while a craft is watched; else null.</summary>
        private static string crafting;

        [HarmonyPrefix]
        public static void Prefix(InventoryGui __instance, Player player)
        {
            crafting = null;
            before.Clear();
            Recipe recipe = __instance.m_craftRecipe;
            if (!CraftingLink.Present || !InventoryState.IsLocal(player) || recipe == null || recipe.m_item == null
                || __instance.m_craftUpgradeItem != null || __instance.m_multiCrafting || BackpackCatalog.Of(recipe.m_item.m_itemData) == null)
                return;
            before.UnionWith(player.GetInventory().GetAllItems());
            crafting = recipe.m_item.m_itemData.m_shared.m_name;
        }

        [HarmonyPostfix]
        public static void Postfix(Player player)
        {
            string name = crafting;
            crafting = null;
            Inventory inventory = player.GetInventory();
            ItemDrop.ItemData from = name != null ? Consumed(inventory) : null;
            ItemDrop.ItemData to = from != null ? Crafted(inventory, name) : null;
            before.Clear();
            if (to == null)
                return;
            Copy(from, to);
            inventory.Changed();   // the new pack's weight may follow its inscriptions (Lightened, Gossamer)
            CraftingHooks.InvalidatePlayer(player);
            Plugin.Log.LogInfo($"the inscriptions of the upgraded {from.m_shared.m_name} went onto the new {to.m_shared.m_name}");
        }

        [HarmonyFinalizer]
        public static void Finalizer()
        {
            crafting = null;
            before.Clear();
        }

        /// <summary>The one magic pack the craft took from the inventory, or null (none, or more than one).</summary>
        private static ItemDrop.ItemData Consumed(Inventory inventory)
        {
            HashSet<ItemDrop.ItemData> now = new HashSet<ItemDrop.ItemData>(inventory.GetAllItems());
            ItemDrop.ItemData found = null;
            foreach (ItemDrop.ItemData item in before)
            {
                if (now.Contains(item) || BackpackCatalog.Of(item) == null || !HasState(item))
                    continue;
                if (found != null)
                    return null;
                found = item;
            }
            return found;
        }

        /// <summary>The one new item of the crafted pack's name, or null.</summary>
        private static ItemDrop.ItemData Crafted(Inventory inventory, string name)
        {
            ItemDrop.ItemData found = null;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (before.Contains(item) || item.m_shared.m_name != name)
                    continue;
                if (found != null)
                    return null;
                found = item;
            }
            return found;
        }

        private static bool HasState(ItemDrop.ItemData item)
        {
            if (item.m_customData == null)
                return false;
            foreach (string key in item.m_customData.Keys)
            {
                if (key.StartsWith(EliteDefinitions.ItemKeyPrefix, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static void Copy(ItemDrop.ItemData from, ItemDrop.ItemData to)
        {
            Dictionary<string, string> data = to.m_customData != null ? new Dictionary<string, string>(to.m_customData) : new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> pair in from.m_customData)
            {
                if (pair.Key.StartsWith(EliteDefinitions.ItemKeyPrefix, StringComparison.Ordinal))
                    data[pair.Key] = pair.Value;
            }
            to.m_customData = data;
        }
    }
}
