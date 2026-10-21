using System.Collections.Generic;
using Mantle.Core;

namespace Mantle.Slots
{
    /// <summary>
    /// Keep On Death (the user's request, by group since 2026-10-07, <see cref="KeptGroups"/>): the items in the slot groups
    /// it names (gear, backpack, utility, trinket, ring, belt, food, mead, ammo, tacklebox) stay with the player when they die; the
    /// other groups go to the grave, and the grave and the next layout put them back as before. Just before the game makes the tombstone they are taken out of the inventory (so
    /// neither its grave nor a world modifier that deletes items at death sees them), with whether each was worn; right
    /// after, they go back into their slots, worn ones marked worn. The game saves the character when the respawn comes
    /// (<c>Game._RequestRespawn</c>, after the tombstone), and its load wears what is marked worn, so the armour and the
    /// utilities are back on at the respawn. The coin purse, the key ring, the tacklebox's cells and the grid follow the
    /// game's rules: keys and bait go to the grave, as they would from anywhere else in the inventory. Local player only.
    /// </summary>
    public sealed class KeptOnDeath
    {
        private readonly List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>();
        private readonly HashSet<ItemDrop.ItemData> worn = new HashSet<ItemDrop.ItemData>();

        /// <summary>Takes the kept slots' items out of the inventory; null when nothing is kept.</summary>
        public static KeptOnDeath Take(Player player)
        {
            KeptGroups keep = InventorySettings.KeepOnDeath.Value;
            if (keep == KeptGroups.None || !InventoryState.Active)
                return null;
            KeptOnDeath kept = new KeptOnDeath();
            Inventory inventory = player.GetInventory();
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                Slot slot = InventoryState.Layout.SlotAt(item.m_gridPos);
                if (slot == null || (keep & GroupOf(slot.Kind)) == KeptGroups.None)
                    continue;
                kept.items.Add(item);
                if (item.m_equipped)
                    kept.worn.Add(item);
                inventory.m_inventory.Remove(item);
            }
            return kept.items.Count > 0 ? kept : null;
        }

        /// <summary>
        /// The group a slot's item is kept with; None for the purse, the key ring and the tacklebox's cells (the box itself
        /// can stay, as a backpack can, and its bait goes to the grave like a backpack's cells) and a retired slot.
        /// </summary>
        private static KeptGroups GroupOf(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.Head: case SlotKind.Chest: case SlotKind.Legs: case SlotKind.Feet: case SlotKind.Back: return KeptGroups.Gear;
                case SlotKind.Backpack: return KeptGroups.Backpack;
                case SlotKind.Utility: return KeptGroups.Utility;
                case SlotKind.Trinket: return KeptGroups.Trinket;
                case SlotKind.Food: return KeptGroups.Food;
                case SlotKind.Mead: return KeptGroups.Mead;
                case SlotKind.Ammo: return KeptGroups.Ammo;
                case SlotKind.Tacklebox: return KeptGroups.Tacklebox;
                case SlotKind.Ring: return KeptGroups.Ring;
                case SlotKind.Belt: return KeptGroups.Belt;
                default: return KeptGroups.None;
            }
        }

        /// <summary>Puts every kept item back in its cell, worn ones marked worn for the respawn's load.</summary>
        public void Return(Player player)
        {
            Inventory inventory = player.GetInventory();
            foreach (ItemDrop.ItemData item in items)
            {
                item.m_equipped = worn.Contains(item);
                if (!inventory.ContainsItem(item))
                    inventory.m_inventory.Add(item);
            }
            inventory.Changed();
            Plugin.Log.LogInfo($"kept {items.Count} slot items through death ({worn.Count} worn)");
        }
    }
}
