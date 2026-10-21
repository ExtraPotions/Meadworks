using System;
using Mantle.Accessories;
using Mantle.Backpacks;
using Mantle.Core;
using Mantle.Ring;
using Mantle.Tackle;

using ItemType = ItemDrop.ItemData.ItemType;

namespace Mantle.Slots
{
    /// <summary>
    /// Which items each slot takes. Worn slots take the game's item types for them (helmet, chest, legs, shoulder,
    /// utility, trinket; Feet EliteEquipment's boots, by prefab name); food is a consumable that fills a food bar, a mead any other consumable; ammo includes the game's
    /// non-equipable ammo (bait); the purse takes coins only; a ring cell only its own key (<see cref="KeyRing"/>); the
    /// Tacklebox slot Mantle's tackleboxes and a box's cell bait (<see cref="TackleRules"/>). The backpack slot takes
    /// Mantle's backpacks (<see cref="BackpackCatalog"/>), an item whose prefab name contains "backpack", or one listed
    /// in Backpack Items: the game has none, other mods do. It is worn for Mantle's own packs, which are equipment
    /// (<see cref="BackpackEquip"/>): Utility items whose worn slot is the Backpack slot, never a Utility slot. The ring and belt cells take only the items the accessory registry
    /// tags for them (<see cref="AccessoryRegistry"/>), and no worn slot takes such an item while the layout has its cell.
    /// </summary>
    public static class SlotRules
    {
        public const string CoinsName = "$item_coins";

        public static bool IsWorn(SlotKind kind) => kind <= SlotKind.Back || kind == SlotKind.Backpack || kind == SlotKind.Utility || kind == SlotKind.Trinket || kind == SlotKind.Feet;

        /// <summary>Whether a slot takes an item: its kind's rule, and for a ring cell the one key of that cell.</summary>
        public static bool Accepts(Slot slot, ItemDrop.ItemData item) =>
            slot.Kind == SlotKind.Key ? item == null || KeyRing.NumberOf(item) == slot.Number : Accepts(slot.Kind, item);

        public static bool Accepts(SlotKind kind, ItemDrop.ItemData item)
        {
            if (item == null)
                return true;
            switch (kind)
            {
                case SlotKind.Backpack: return IsBackpack(item);
                case SlotKind.Food: return IsFood(item);
                case SlotKind.Mead: return Is(item, ItemType.Consumable) && !IsFood(item);
                case SlotKind.Ammo: return Is(item, ItemType.Ammo) || Is(item, ItemType.AmmoNonEquipable);
                case SlotKind.Purse: return IsCoins(item);
                case SlotKind.Key: return KeyRing.IsKey(item);
                case SlotKind.Tacklebox: return TackleboxCatalog.Of(item) != null;
                case SlotKind.Tackle: return TackleRules.IsTackle(item);
                case SlotKind.Ring: return AccessoryItems.KindOf(item) == SlotKind.Ring;
                case SlotKind.Belt: return AccessoryItems.KindOf(item) == SlotKind.Belt;
                case SlotKind.Retired: return false;
                default: return WornKindOf(item) == kind;
            }
        }

        /// <summary>The worn slot kind of an item, or null when no worn slot takes it.</summary>
        public static SlotKind? WornKindOf(ItemDrop.ItemData item)
        {
            if (AccessoryBonus.Claims(item))
                return null;   // a tagged item (a ring, a belt, the Megingjord) lives in its own slot, never a worn one
            if (BackpackCatalog.Of(item) != null)
                return SlotKind.Backpack;   // Mantle's packs are Utility items worn in the Backpack slot (BackpackEquip)
            if (EliteEquipmentLink.IsBoots(item))
                return SlotKind.Feet;   // EliteEquipment's boots are Legs items, told by prefab name: Feet only, never Legs
            switch (item.m_shared.m_itemType)
            {
                case ItemType.Helmet: return SlotKind.Head;
                case ItemType.Chest: return SlotKind.Chest;
                case ItemType.Legs: return SlotKind.Legs;
                case ItemType.Shoulder: return SlotKind.Back;
                case ItemType.Utility: return SlotKind.Utility;
                case ItemType.Trinket: return SlotKind.Trinket;
                default: return null;
            }
        }

        public static bool IsCoins(ItemDrop.ItemData item) => item.m_shared.m_name == CoinsName;

        public static bool IsFood(ItemDrop.ItemData item)
        {
            ItemDrop.ItemData.SharedData shared = item.m_shared;
            return shared.m_itemType == ItemType.Consumable && (shared.m_food > 0f || shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        }

        public static bool IsBackpack(ItemDrop.ItemData item)
        {
            if (BackpackCatalog.Of(item) != null)
                return true;   // Mantle's own packs: not all have "backpack" in their prefab name (Mantle_LoxHauler)
            string prefab = item.m_dropPrefab != null ? item.m_dropPrefab.name : "";
            if (prefab.IndexOf("backpack", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            foreach (string listed in InventorySettings.BackpackItems.Value.Split(','))
            {
                if (listed.Trim().Length > 0 && string.Equals(listed.Trim(), prefab, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool Is(ItemDrop.ItemData item, ItemType type) => item.m_shared.m_itemType == type;
    }
}
