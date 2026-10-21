using HarmonyLib;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Ring
{
    /// <summary>
    /// Clicks on the key ring. On the ring button: a click opens or closes the pop-up; a click while dragging a key puts
    /// the key into its own ring cell, open or shut, anything else is refused with a message. A dragged key let go on any
    /// cell of the pop-up also goes to its own cell (<c>InventoryGui.OnSelectedItem</c>, rewritten before the slot rules
    /// look at it, <see cref="DragSettle"/>); anything else let go there is refused by the slot rules. The gamepad reaches
    /// the shut ring through the hidden first ring cell (<see cref="KeyRingGamepad"/>): A or X there works as a click on
    /// the button, never on the hidden key.
    /// </summary>
    public static class KeyRingClicks
    {
        public static void OnButton(InventoryGui gui)
        {
            if (gui == null || !KeyRing.Active)
                return;
            if (gui.m_dragGo == null)
            {
                KeyRingState.Toggle();
                return;
            }
            Vector2i cell = KeyRing.CellOf(gui.m_dragItem);
            if (cell.x < 0)
            {
                Messages.Center(Words.NotAKey);
                return;
            }
            ItemDrop.ItemData there = InventoryState.Player.GetInventory().GetItemAt(cell.x, cell.y);
            gui.OnSelectedItem(gui.m_playerGrid, there, cell, InventoryGrid.Modifier.Select);
        }

        /// <summary>A ring cell of the player grid, while the ring is laid out.</summary>
        private static Slot RingSlot(InventoryGui gui, InventoryGrid grid, Vector2i pos)
        {
            Slot slot = grid == gui.m_playerGrid ? InventoryState.SlotAt(grid.GetInventory(), pos) : null;
            return slot != null && slot.Kind == SlotKind.Key ? slot : null;
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
        public static class Selected
        {
            [HarmonyPrefix]
            [HarmonyPriority(Priority.First)]
            public static bool Prefix(InventoryGui __instance, InventoryGrid grid, ref ItemDrop.ItemData item, ref Vector2i pos)
            {
                if (RingSlot(__instance, grid, pos) == null)
                    return true;
                if (__instance.m_dragGo != null)
                {
                    ToOwnCell(__instance, grid, ref item, ref pos);
                    return true;
                }
                if (KeyRingState.Open)
                    return true;
                KeyRingState.Toggle();
                return false;
            }

            private static void ToOwnCell(InventoryGui gui, InventoryGrid grid, ref ItemDrop.ItemData item, ref Vector2i pos)
            {
                Vector2i own = KeyRing.CellOf(gui.m_dragItem);
                if (own.x < 0 || own == pos)
                    return;
                pos = own;
                item = grid.GetInventory().GetItemAt(own.x, own.y);
            }
        }

        [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnRightClickItem))]
        public static class RightClick
        {
            [HarmonyPrefix]
            public static bool Prefix(InventoryGui __instance, InventoryGrid grid, Vector2i pos)
            {
                if (RingSlot(__instance, grid, pos) == null || KeyRingState.Open)
                    return true;
                KeyRingState.Toggle();
                return false;
            }
        }
    }
}
