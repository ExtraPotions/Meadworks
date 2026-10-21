using System.Collections.Generic;
using HarmonyLib;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Worn
{
    /// <summary>
    /// Auto Equip: a right click on a piece of gear in an open chest (the game's use of an item, which does nothing for
    /// equipment outside the player's own inventory) wears it. The piece in its slot comes off first and goes where any
    /// piece taken off goes (<see cref="GearOut"/>: a free cell; with none it stays on and nothing changes), then the
    /// chest's piece moves into the slot as a drag would move it and is put on. A utility takes a free Utility slot while it can be worn beside the
    /// others, else the slot of the one it replaces. The chest is open, so this client owns it, as for a drag.
    /// </summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UseItem))]
    public static class ChestWear
    {
        [HarmonyPrefix]
        public static bool Prefix(Humanoid __instance, Inventory inventory, ItemDrop.ItemData item, bool fromInventoryGui)
        {
            if (!fromInventoryGui || inventory == null || item == null || !GearKeep.On || !InventoryState.IsLocal(__instance))
                return true;
            if (InventoryState.Manages(inventory) || !inventory.ContainsItem(item))
                return true;
            SlotKind? kind = SlotRules.WornKindOf(item);
            if (kind == null || InventoryState.CellsOf(kind.Value).Count == 0 || !Wearable(__instance, item, kind.Value))
                return true;
            Wear(__instance, inventory, item, kind.Value);
            return false;
        }

        /// <summary>A backpack is worn by lying in its slot (no game guards); the rest pass the game's equip guards.</summary>
        private static bool Wearable(Humanoid humanoid, ItemDrop.ItemData item, SlotKind kind) =>
            kind == SlotKind.Backpack ? BackpackCatalog.Of(item) != null && BackpackSettings.Active : WearCheck.Guards(humanoid, item, tell: true);

        private static void Wear(Humanoid humanoid, Inventory chest, ItemDrop.ItemData item, SlotKind kind)
        {
            Inventory own = humanoid.GetInventory();
            Vector2i cell = Target(humanoid, item, kind);
            ItemDrop.ItemData there = own.GetItemAt(cell.x, cell.y);
            if (there != null && !GearOut.HasRoom(own, kind))
            {
                Messages.Center(Words.NoRoomOff);
                return;
            }
            if (there != null)
                TakeOut(humanoid, there, kind);
            if (own.GetItemAt(cell.x, cell.y) != null)
                return;
            own.MoveItemToThis(chest, item, item.m_stack, cell.x, cell.y);
            ItemDrop.ItemData moved = own.GetItemAt(cell.x, cell.y);
            if (moved != null)
                humanoid.EquipItem(moved);
        }

        /// <summary>The slot the piece goes into; for a utility a free (or unworn) one while it is worn beside the others.</summary>
        private static Vector2i Target(Humanoid humanoid, ItemDrop.ItemData item, SlotKind kind)
        {
            IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(kind);
            if (kind != SlotKind.Utility)
                return cells[0];
            if (WearCheck.Beside(humanoid, item))
                return BesideCell(humanoid, cells);
            Vector2i replaced = WearCheck.Replaced(humanoid, item, cells);
            return replaced.x >= 0 ? replaced : cells[0];
        }

        /// <summary>The first empty Utility slot, else the first holding an unworn item, else the first.</summary>
        private static Vector2i BesideCell(Humanoid humanoid, IReadOnlyList<Vector2i> cells)
        {
            Inventory inventory = humanoid.GetInventory();
            Vector2i unworn = cells[0];
            bool found = false;
            for (int i = 0; i < cells.Count; i++)
            {
                ItemDrop.ItemData there = inventory.GetItemAt(cells[i].x, cells[i].y);
                if (there == null)
                    return cells[i];
                if (!found && !humanoid.IsItemEquiped(there))
                {
                    unworn = cells[i];
                    found = true;
                }
            }
            return unworn;
        }

        /// <summary>
        /// The piece in the slot off and out now (not at the next frame, the slot is needed): taken off with the slot moves
        /// held back, then into a free cell.
        /// </summary>
        private static void TakeOut(Humanoid humanoid, ItemDrop.ItemData there, SlotKind kind)
        {
            if (humanoid.IsItemEquiped(there))
            {
                WornPlacement.Suspend();
                try
                {
                    humanoid.UnequipItem(there);
                }
                finally
                {
                    WornPlacement.Resume();
                }
            }
            GearOut.Away(humanoid, there, kind);
        }
    }
}
