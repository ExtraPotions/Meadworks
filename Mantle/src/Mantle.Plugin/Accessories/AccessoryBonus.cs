// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;

namespace Mantle.Accessories
{
    /// <summary>
    /// What the local player's rings and belt give right now. Nothing is stored: every answer is worked out from the items
    /// that lie in the Ring and Belt cells at that moment (<see cref="AccessoryRegistry.Total"/>), and only while the layout
    /// is in use for the local, living player in a ready world. So a slot insert or removal, a death, a logout and a world
    /// change need no equip or clean-up step, a load cannot apply a bonus twice, and two identical rings give double. The
    /// game's own questions (carry weight, regeneration, speed, attack stamina, the food totals that set the maximum
    /// health, stamina and eitr) are answered through <see cref="AccessoryPatches"/>. A game belt that is worn (<see cref="BeltWear"/>) is left out:
    /// its status effect gives the same stats, so they count once.
    /// </summary>
    public static class AccessoryBonus
    {
        private static readonly SlotKind[] Kinds = { SlotKind.Ring, SlotKind.Belt };

        /// <summary>The bonus of the local player's accessory cells; none when no layout is in use for a living local player.</summary>
        public static AccessoryStats Current()
        {
            Player player = InventoryState.Player;
            if (!InventoryState.Active || player == null || player != Player.m_localPlayer || player.IsDead())
                return AccessoryStats.None;
            Inventory inventory = player.GetInventory();
            AccessoryRegistry registry = AccessoryRegistry.Current;
            AccessoryStats total = AccessoryStats.None;
            foreach (SlotKind kind in Kinds)
            {
                AccessorySlot slot = kind == SlotKind.Ring ? AccessorySlot.Ring : AccessorySlot.Belt;
                // Indexed: this runs on every carry, food, stamina and speed query, and a foreach over the list boxes its enumerator.
                IReadOnlyList<Vector2i> cells = InventoryState.CellsOf(kind);
                for (int i = 0; i < cells.Count; i++)
                {
                    Vector2i cell = cells[i];
                    ItemDrop.ItemData item = inventory.GetItemAt(cell.x, cell.y);
                    if (item != null && !(item.m_equipped && WornNatively(item)))   // a worn game belt gives its own effects
                        total = registry.Add(total, slot, ItemNames.PrefabName(item));
                }
            }
            return total;
        }

        /// <summary>
        /// Whether the slots take an item over the game's own rules for it: it is tagged, and the layout in use has a cell of its
        /// kind. With the belt slot off (or Mantle off) a tagged Megingjord is the ordinary utility item it always was.
        /// </summary>
        public static bool Claims(ItemDrop.ItemData item)
        {
            SlotKind? kind = AccessoryItems.KindOf(item);
            return kind.HasValue && InventoryState.Active && InventoryState.CellsOf(kind.Value).Count > 0;
        }

        /// <summary>
        /// A game utility item (the Megingjord) tagged for the belt slot: it is worn through the game's own effects while it lies in the
        /// belt cell (<see cref="BeltWear"/>), so its registry stats are not added on top. Mantle's own belts are never worn.
        /// </summary>
        public static bool WornNatively(ItemDrop.ItemData item) =>
            item != null && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility && AccessoryItems.KindOf(item) == SlotKind.Belt;
    }
}
