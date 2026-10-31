using System.Collections.Generic;
using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;
using Mantle.Tackle;
using Mantle.Worn;

namespace Mantle.Crafting
{
    /// <summary>
    /// A backpack or tacklebox upgraded in its slot (the user's request, 2026-10-04): when the recipe takes the pack in the
    /// Backpack slot (the box in the Tacklebox slot), the new one goes straight into that slot, so the craft needs no free
    /// cell and the pack's rows stay open with everything in them. The game alone would ask for a free main cell (with a
    /// full grid: "Inventory full", nothing crafted), put the new pack there and consume the old one out of the slot,
    /// closing its rows and moving or dropping what lay in them. For the length of the game's craft the old pack is set
    /// aside off the grid, so the emptied slot is the free cell the game finds for the new one, and the game's own cost
    /// consumes the old one (put first in the inventory's list, so the cost takes it rather than a spare copy in the
    /// grid). One that was not consumed (the craft stopped, or the game took a spare copy from the grid)
    /// goes back into the slot, or into a free cell when the new one is there. Not for multi-crafting or crafting
    /// without cost, which consume no or several old ones.
    /// The game's own quality upgrade of the worn pack (its levels, <see cref="BackpackLevels"/>) takes the pack off,
    /// removes it and adds the new one at its cell. Taken off, the pack would move out of its slot to a main cell
    /// (<see cref="WornPlacement"/>), and with a full grid the new one would find no free cell and be lost with the old:
    /// so the placement waits for the craft, and the emptied slot is the free cell.
    /// </summary>
    public static class InPlaceUpgrade
    {
        private static readonly Vector2i Aside = new Vector2i(-1, -1);
        private static readonly List<Vector2i> none = new List<Vector2i>();

        /// <summary>Inside the game's craft button press or craft: the two places that ask whether the new item fits.</summary>
        private static bool crafting;
        internal static bool IsCrafting => crafting;

        /// <summary>The old item set aside during the game's craft, and the slot cell it came from.</summary>
        private static ItemDrop.ItemData old;
        private static Vector2i slot;

        /// <summary>Inside the game's quality upgrade of the worn pack, whose slot is <see cref="slot"/>.</summary>
        private static bool upgrading;

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnCraftPressed))]
        public static class Press
        {
            [HarmonyPrefix]
            public static void Prefix() => crafting = true;

            [HarmonyFinalizer]
            public static void Finalizer() => crafting = false;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
        public static class Craft
        {
            [HarmonyPrefix]
            public static void Prefix(InventoryGui __instance, Player player)
            {
                crafting = true;
                if (LevelUp(__instance, player))
                    return;
                ItemDrop.ItemData there = Upgraded(__instance, player, out slot);
                if (there == null)
                    return;
                old = there;
                there.m_gridPos = Aside;
                TakenFirst(player.GetInventory(), there);
            }

            [HarmonyFinalizer]
            public static void Finalizer(Player player)
            {
                crafting = false;
                if (upgrading)
                {
                    upgrading = false;
                    WornPlacement.Resume();
                }
                ItemDrop.ItemData leftover = old;
                old = null;
                if (leftover != null && player.GetInventory().ContainsItem(leftover))
                    PutBack(player, leftover);
            }
        }

        /// <summary>Inventory.CanAddItem: the crafted item fits while it takes the place of the item it upgrades or fits an empty dedicated slot.</summary>
        public static bool Fits(Inventory inventory, ItemDrop.ItemData item)
        {
            InventoryGui gui = InventoryGui.instance;
            if (!crafting || gui == null || gui.m_craftRecipe == null || gui.m_craftRecipe.m_item == null
                || gui.m_craftRecipe.m_item.m_itemData.m_shared.m_name != item.m_shared.m_name)
                return false;
            if (old != null ? inventory.GetItemAt(slot.x, slot.y) == null : Upgraded(gui, InventoryState.Player, out _) != null)
                return true;
            if (InventoryState.Layout != null)
            {
                var legal = MainCells.LegalExtraCells(InventoryState.Layout, item);
                for (int i = 0; i < legal.Count; i++)
                    if (inventory.GetItemAt(legal[i].x, legal[i].y) == null)
                        return true;
            }
            return false;
        }

        /// <summary>Inventory.FindEmptySlot: during the in-place upgrade of a backpack or tacklebox the emptied slot is the free cell.</summary>
        public static bool FreeSlot(Inventory inventory, out Vector2i cell)
        {
            cell = slot;
            return (old != null || upgrading) && inventory.GetItemAt(slot.x, slot.y) == null;
        }

        /// <summary>
        /// Inventory.FindEmptySlot when the main grid has no free cell: during the game's craft an empty dedicated slot
        /// that takes the crafted item is the free cell, so a full grid does not stop the craft (<see cref="Fits"/>).
        /// </summary>
        public static bool SlotWhenFull(Inventory inventory, out Vector2i cell)
        {
            cell = Aside;
            if (crafting && InventoryState.Layout != null && InventoryGui.instance != null)
            {
                var gui = InventoryGui.instance;
                var itemData = gui.m_craftUpgradeItem ?? gui.m_craftRecipe?.m_item?.m_itemData;
                if (itemData != null)
                {
                    var legal = MainCells.LegalExtraCells(InventoryState.Layout, itemData);
                    for (int i = 0; i < legal.Count; i++)
                    {
                        if (inventory.GetItemAt(legal[i].x, legal[i].y) == null)
                        {
                            cell = legal[i];
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>The game's quality upgrade of the pack in the Backpack slot: the placement waits, the slot is noted.</summary>
        private static bool LevelUp(InventoryGui gui, Player player)
        {
            ItemDrop.ItemData item = gui.m_craftUpgradeItem;
            if (item == null || BackpackCatalog.Of(item) == null || !InventoryState.IsLocal(player) || !InventoryState.Active)
                return false;
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Backpack);
            if (cells.Count == 0 || player.GetInventory().GetItemAt(cells[0].x, cells[0].y) != item)
                return false;
            slot = cells[0];
            upgrading = true;
            WornPlacement.Suspend();
            return true;
        }

        /// <summary>The item in the Backpack or Tacklebox slot that the craft under way takes as a cost, or null.</summary>
        private static ItemDrop.ItemData Upgraded(InventoryGui gui, Player player, out Vector2i cell)
        {
            cell = Aside;
            Recipe recipe = gui.m_craftRecipe;
            if (!InventoryState.IsLocal(player) || !InventoryState.Active || recipe == null || recipe.m_item == null
                || gui.m_craftUpgradeItem != null || gui.m_multiCrafting || WithoutCost(player))
                return null;
            IReadOnlyList<Vector2i> cells = SlotCells(recipe.m_item.m_itemData);
            if (cells.Count == 0)
                return null;
            cell = cells[0];
            ItemDrop.ItemData there = player.GetInventory().GetItemAt(cell.x, cell.y);
            return there != null && Takes(recipe, there) ? there : null;
        }

        /// <summary>The slot a crafted backpack or tacklebox is worn in, while the layout has it.</summary>
        private static IReadOnlyList<Vector2i> SlotCells(ItemDrop.ItemData crafted)
        {
            if (BackpackCatalog.Of(crafted) != null)
                return InventoryState.CellsOf(SlotKind.Backpack);
            if (TackleboxCatalog.Of(crafted) != null)
                return InventoryState.CellsOf(SlotKind.Tacklebox);
            return none;
        }

        private static bool Takes(Recipe recipe, ItemDrop.ItemData item)
        {
            foreach (Piece.Requirement need in recipe.m_resources)
            {
                if (need.m_resItem != null && need.m_resItem.m_itemData.m_shared.m_name == item.m_shared.m_name && need.GetAmount(1) > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The game's cost takes copies in the inventory's list order (<c>Inventory.RemoveItem(name, ...)</c>), so the pack
        /// set aside goes first in the list: the worn pack is the one used, and a magic one's EliteCrafting inscriptions
        /// carry over to the new pack (<see cref="Elite.MagicCarryOver"/>) rather than a spare copy's in the grid.
        /// </summary>
        private static void TakenFirst(Inventory inventory, ItemDrop.ItemData item)
        {
            if (inventory.m_inventory.Remove(item))
                inventory.m_inventory.Insert(0, item);
        }

        private static bool WithoutCost(Player player) =>
            player.NoCostCheat() || (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost));

        /// <summary>Back into its slot, or into a free main cell when the new item lies there; dropped only when neither is free.</summary>
        private static void PutBack(Player player, ItemDrop.ItemData item)
        {
            Inventory inventory = player.GetInventory();
            Vector2i cell = inventory.GetItemAt(slot.x, slot.y) == null ? slot : MainCells.FindEmpty(inventory, InventoryState.Layout, topFirst: false);
            if (cell.x < 0)
            {
                player.DropItem(inventory, item, item.m_stack);
                return;
            }
            item.m_gridPos = cell;
            inventory.Changed();
        }
    }
}
