using System;
using HarmonyLib;
using Mantle.Core;
using Mantle.Worn;

namespace Mantle.Slots
{
    /// <summary>
    /// A drag and drop on the inventory screen (<c>InventoryGui.OnSelectedItem</c>). The game unequips both items
    /// before it drops and equips them again afterwards wherever they landed, so the worn slots are left alone during
    /// the click and sorted out after it: an item that landed in its worn slot is put on, a worn item that left the
    /// worn slots is taken off. A drop that breaks a slot rule is refused before the game starts (the game would
    /// otherwise equip whatever lay under the pointer), with a message; the dragged item stays in hand.
    /// </summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnSelectedItem))]
    public static class DragSettle
    {
        public sealed class Pair
        {
            public ItemDrop.ItemData Dragged;
            public ItemDrop.ItemData Target;
        }

        /// <summary>An earlier prefix that already refused the click (a full tacklebox, with its own message) is left alone.</summary>
        [HarmonyPrefix]
        public static bool Prefix(InventoryGui __instance, InventoryGrid grid, ItemDrop.ItemData item, Vector2i pos, out Pair __state, bool __runOriginal)
        {
            __state = null;
            if (!__runOriginal)
                return false;
            bool dragging = __instance.m_dragGo != null;
            if (dragging && !SlotDrop.Allowed(grid.GetInventory(), __instance.m_dragInventory, __instance.m_dragItem, __instance.m_dragAmount, pos))
            {
                Messages.Center(Words.WrongSlot);
                return false;
            }
            if (!InventoryState.Active)
                return true;
            WornPlacement.Suspend();
            __state = new Pair { Dragged = dragging ? __instance.m_dragItem : null, Target = item };
            return true;
        }

        [HarmonyPostfix]
        public static void Postfix(Pair __state)
        {
            if (__state == null)
                return;
            WornPlacement.Resume();
            Settle(__state.Dragged);
            Settle(__state.Target);
        }

        [HarmonyFinalizer]
        public static void Finalizer(Pair __state, Exception __exception)
        {
            if (__state != null && __exception != null)
                WornPlacement.Resume();
        }

        private static void Settle(ItemDrop.ItemData item)
        {
            Player player = InventoryState.Player;
            if (item == null || !InventoryState.Active || !player.GetInventory().ContainsItem(item))
                return;
            Slot slot = InventoryState.Layout.SlotAt(item.m_gridPos);
            SlotKind? kind = SlotRules.WornKindOf(item);
            if (slot != null && SlotRules.IsWorn(slot.Kind) && kind == slot.Kind)
            {
                if (!item.m_equipped)
                    player.EquipItem(item);
                return;
            }
            if (item.m_equipped && kind != null && InventoryState.CellsOf(kind.Value).Count > 0)
                player.UnequipItem(item);
        }
    }
}
