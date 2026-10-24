using System.Collections.Generic;
using HarmonyLib;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Tackle
{
    /// <summary>
    /// A dragged bait let go on the Tacklebox slot goes into the box, open or shut, as a key let go on the ring's button
    /// goes onto the ring: onto a stack of the same bait with room, else into the first empty cell; a full box says so and
    /// the bait stays in hand. Rewritten before the slot rules look at the drop (<see cref="DragSettle"/>), so the slot
    /// itself still takes only tackleboxes, and a box dropped there swaps with the one in it.
    /// With nothing in hand, a plain click (or the gamepad's A) on the box in its slot opens or shuts the pop-up (playtest
    /// 2026-10-20: left click, as the key ring's button); a right click takes it off (<see cref="TackleboxUse"/>). A
    /// Shift click picks the box up as a plain click did before, so it can still be moved out of the slot; a Ctrl click
    /// and a drop of another box onto the slot work as before.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    public static class TackleClicks
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(InventoryGui __instance, InventoryGrid grid, ref ItemDrop.ItemData item, ref Vector2i pos, ref InventoryGrid.Modifier __3)
        {
            if (grid != __instance.m_playerGrid || !Tacklebox.Active)
                return true;
            if (__instance.m_dragGo == null)
                return OnBox(grid, item, pos, ref __3);
            Slot slot = InventoryState.SlotAt(grid.GetInventory(), pos);
            if (slot == null || slot.Kind != SlotKind.Tacklebox || !TackleRules.IsTackle(__instance.m_dragItem))
                return true;
            Vector2i cell = Target(grid.GetInventory(), __instance.m_dragItem);
            if (cell.x < 0)
            {
                Messages.Center(TackleboxWords.Full);
                return false;
            }
            pos = cell;
            item = grid.GetInventory().GetItemAt(cell.x, cell.y);
            return true;
        }

        /// <summary>
        /// A click on the box in its Tacklebox slot with nothing in hand: Select toggles the pop-up, Split (Shift) is turned
        /// into Select so the game picks the box up, anything else is the game's. Elsewhere the click is the game's.
        /// </summary>
        private static bool OnBox(InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, ref InventoryGrid.Modifier mod)
        {
            if (item == null || TackleboxCatalog.Of(item) == null)
                return true;
            Slot slot = InventoryState.SlotAt(grid.GetInventory(), pos);
            if (slot == null || slot.Kind != SlotKind.Tacklebox)
                return true;
            if (mod == InventoryGrid.Modifier.Split)
            {
                mod = InventoryGrid.Modifier.Select;
                return true;
            }
            if (mod != InventoryGrid.Modifier.Select)
                return true;
            TackleState.Toggle();
            return false;
        }

        /// <summary>A stack of the same bait with room, else an empty cell; (-1, -1) when the box has neither.</summary>
        private static Vector2i Target(Inventory inventory, ItemDrop.ItemData dragged)
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(SlotKind.Tackle);
            Vector2i empty = new Vector2i(-1, -1);
            foreach (Vector2i cell in cells)
            {
                ItemDrop.ItemData there = inventory.GetItemAt(cell.x, cell.y);
                if (there == null && empty.x < 0)
                    empty = cell;
                bool same = there != null && there != dragged && there.IsSameType(dragged) && there.m_quality == dragged.m_quality;
                if (same && there.m_stack < there.m_shared.m_maxStackSize)
                    return cell;
            }
            return empty;
        }
    }
}
