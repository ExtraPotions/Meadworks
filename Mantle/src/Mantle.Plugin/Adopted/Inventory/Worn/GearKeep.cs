using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Worn
{
    /// <summary>
    /// Auto Equip (the user's request, 2026-10-05: "if gear is in the gear tab it auto equips"), in the local player's
    /// frame: an unworn piece lying in its worn slot is put on (one dragged in from a chest, which the game copies so that
    /// <see cref="DragSettle"/> never sees it; one left after an attack or a swim refused it). Two are left off until they
    /// leave their slot or are put on again: one whose equip another mod refuses, and one taken off with no free main
    /// cell, which comes off in place (the user, 2026-10-06: "if auto equip is on, inventory is full, and you want to
    /// unequip helmet, it should unequip in place"). Backpacks keep their own rule (<see cref="Backpacks.BackpackEquip"/>).
    /// Boots in the Feet slot are put on through the game's EquipItem, which EliteEquipment answers for them.
    /// Mantle: ring and belt items are the accessory slots' own (<see cref="SlotRules.WornKindOf"/> answers none for them),
    /// so Auto Equip never touches them; the belt cell keeps its own rule (<see cref="Accessories.BeltWear"/>).
    /// </summary>
    public static class GearKeep
    {
        private static readonly SlotKind[] Kinds = { SlotKind.Head, SlotKind.Chest, SlotKind.Legs, SlotKind.Feet, SlotKind.Back, SlotKind.Utility, SlotKind.Trinket };
        private static readonly List<ItemDrop.ItemData> leftOff = new List<ItemDrop.ItemData>();

        public static bool On => InventoryState.Active && InventorySettings.AutoEquip.Value;

        /// <summary>A piece taken off in its slot (no free main cell): not put on again while it lies there unworn.</summary>
        public static void LeaveOff(ItemDrop.ItemData item)
        {
            if (!leftOff.Contains(item))
                leftOff.Add(item);
        }

        public static void Tick(Player player)
        {
            if (!On || player.IsDead())
            {
                leftOff.Clear();
                return;
            }
            if (WornPlacement.Suspended || Dragging())
                return;
            Forget(player);
            WearAll(player);
        }

        /// <summary>A piece left off that left its slot (or was put on another way) may be put on again.</summary>
        private static void Forget(Player player)
        {
            for (int i = leftOff.Count - 1; i >= 0; i--)
            {
                if (!GearOut.LiesUnworn(player, leftOff[i], out _))
                    leftOff.RemoveAt(i);
            }
        }

        private static void WearAll(Player player)
        {
            Inventory inventory = player.GetInventory();
            foreach (SlotKind kind in Kinds)
            {
                IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(kind);
                for (int i = 0; i < cells.Count; i++)
                    Wear(player, inventory.GetItemAt(cells[i].x, cells[i].y), kind);
            }
        }

        private static void Wear(Player player, ItemDrop.ItemData item, SlotKind kind)
        {
            if (item == null || SlotRules.WornKindOf(item) != kind || player.IsItemEquiped(item) || leftOff.Contains(item))
                return;
            if (!WearCheck.Guards(player, item, tell: false) || !WearCheck.Beside(player, item))
                return;
            if (!player.EquipItem(item))
                leftOff.Add(item);
        }

        private static bool Dragging() => InventoryGui.instance != null && InventoryGui.instance.m_dragGo != null;
    }
}
