// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using Mantle.Core;
using Mantle.Slots;
using Mantle.Worn;

namespace Mantle.Accessories
{
    /// <summary>
    /// The game's own belt (the Megingjord, or any game utility item the registry tags for the belt slot) lying in the belt cell is
    /// worn like the game wears it: the belt shows on the character, its status effect runs and shows in the HUD, and that effect
    /// is what carries the +150, so <see cref="AccessoryBonus"/> leaves a worn one out and the carry counts exactly once. It is worn
    /// beside the utility items (<see cref="ExtraUtilities"/>, not counted against the utility slots, the game's utility field stays
    /// free), and it is worn exactly while it lies in the belt cell. Run every frame for the local player, after a layout was applied
    /// and after a right click puts an item into or out of its cell; each call moves the worn state towards the cells and then
    /// has nothing to do, so it never applies an effect twice. Items the slots claim are never left worn any other way: a
    /// tagged utility item that was worn as the game's utility, a ring, or an item of any other type.
    /// </summary>
    public static class BeltWear
    {
        /// <summary>The game belt lying in the belt cell now, or null.</summary>
        public static ItemDrop.ItemData Wanted(Inventory inventory)
        {
            foreach (Vector2i cell in InventoryState.CellsOf(SlotKind.Belt))
            {
                ItemDrop.ItemData item = inventory.GetItemAt(cell.x, cell.y);
                if (item != null && AccessoryBonus.WornNatively(item))
                    return item;
            }
            return null;
        }

        public static void Reconcile(Humanoid humanoid, bool equipEffects = false)
        {
            if (!InventoryState.IsLocal(humanoid) || !InventoryState.Active || humanoid.IsDead())
                return;
            Inventory inventory = humanoid.GetInventory();
            ItemDrop.ItemData belt = Wanted(inventory);

            ItemDrop.ItemData worn = ExtraUtilities.Belt;
            if (worn != null && worn != belt)
            {
                TakeOff(humanoid, worn);
                if (inventory.ContainsItem(worn) && !AccessoryBonus.Claims(worn) && (humanoid.m_utilityItem == null || ExtraUtilities.Takes(humanoid, worn)))
                    humanoid.EquipItem(worn, false);   // the belt slot is gone: the vanilla utility item again, unless five utilities already fill the slots
            }
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item.m_equipped && item != belt && AccessoryBonus.Claims(item))
                    TakeOff(humanoid, item);
            }
            if (belt == null)
                return;
            if (belt.m_equipped && !ExtraUtilities.IsWorn(belt))
                TakeOff(humanoid, belt);   // it was the game's utility item (Belt Slot just turned on): move it to the belt
            if (!belt.m_equipped)
                ExtraUtilities.WearBelt(humanoid, belt, equipEffects);
        }

        /// <summary>The prefab the character shows as its utility item: the worn belt, else null (the game's own choice stands).</summary>
        public static string LookName(Humanoid humanoid)
        {
            ItemDrop.ItemData belt = ExtraUtilities.Belt;
            return belt != null && belt.m_equipped && InventoryState.IsLocal(humanoid) ? ItemNames.PrefabName(belt) : null;
        }

        private static void TakeOff(Humanoid humanoid, ItemDrop.ItemData item)
        {
            if (humanoid.IsItemEquiped(item))
                humanoid.UnequipItem(item, triggerEquipEffects: false);
            else
                item.m_equipped = false;   // a saved flag no field backs
        }
    }
}
