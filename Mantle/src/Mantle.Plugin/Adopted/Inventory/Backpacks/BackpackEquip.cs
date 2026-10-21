using System.Collections.Generic;
using Mantle.Core;
using Mantle.Slots;
using UnityEngine;

namespace Mantle.Backpacks
{
    /// <summary>
    /// A backpack is equipment (the user's request, 2026-10-04: "can it be an equipment? and can we make it work with epic
    /// loot?"): a Utility item, so the game counts it equipable and a mod that reads the equipment sees it (Epic Loot
    /// enchants it, rolls its utility effects on it and applies them while it is worn), but Mantle wears it, never the
    /// game's utility field. The pack in the Backpack slot is the worn one: it carries the game's equipped flag (saved with
    /// the inventory, the grid's equipped mark, <c>Inventory.GetEquippedItems</c>), every other pack does not. EquipItem on
    /// a pack wears it here (the pack worn before comes off, <see cref="Worn.WornPlacement"/> moves the new one into the
    /// slot); the game's own UnequipItem takes it off, since <c>IsItemEquiped</c> answers yes for it. <see cref="Sync"/>
    /// keeps the flag on what lies in the slot after every move that is no equip (a drag, the grave, an upgrade in place).
    /// </summary>
    public static class BackpackEquip
    {
        /// <summary>The pack worn at the last frame, to notice one that left the inventory without coming off.</summary>
        private static ItemDrop.ItemData lastWorn;

        /// <summary>EquipItem on one of Mantle's packs is always ours, on any character, so a pack is never the game's utility.</summary>
        public static bool Takes(ItemDrop.ItemData item) => BackpackCatalog.Of(item) != null;

        /// <summary>
        /// Wears the pack; true when it is worn afterwards. False where Mantle lays out no Backpack slot (off, the
        /// backpacks off, another character, the main menu) or the pack is not in the player's inventory. None of the
        /// game's equip guards (attacking, swimming, world level): the pack in the slot is worn, as its slots are.
        /// </summary>
        public static bool Wear(Humanoid humanoid, ItemDrop.ItemData item, bool triggerEquipEffects)
        {
            if (!CanWear(humanoid, item))
                return false;
            if (item.m_equipped)
                return true;   // the load's EquipInventoryItems: worn as saved
            TakeOffOthers(humanoid, item);
            item.m_equipped = true;
            Transform transform = humanoid.transform;
            item.m_shared.m_equipEffect.Create(transform.position + Vector3.up, transform.rotation, null, 1f, -1, humanoid.GetZDOID());
            humanoid.SetupEquipment();
            if (triggerEquipEffects)
                humanoid.TriggerEquipEffect(item);
            return true;
        }

        /// <summary>IsItemEquiped for a pack: its flag, in the local player's own inventory.</summary>
        public static bool IsWorn(Humanoid humanoid, ItemDrop.ItemData item) =>
            item != null && item.m_equipped && BackpackCatalog.Of(item) != null && InventoryState.IsLocal(humanoid)
            && humanoid.GetInventory().ContainsItem(item);

        /// <summary>Every pack off through the game's UnequipItem: unequip all (death, so the pack goes to the grave as armour does).</summary>
        public static void TakeOffAll(Humanoid humanoid) => TakeOffOthers(humanoid, null);

        /// <summary>
        /// The local player's frame: the pack in the Backpack slot is worn, any other is not, through the game's EquipItem
        /// and UnequipItem so Epic Loot and the like hear of it. Nothing is worn while Mantle or the backpacks are off.
        /// </summary>
        public static void Sync(Player player)
        {
            ItemDrop.ItemData inSlot = Backpack.WornItem(player);
            TakeOffOthers(player, inSlot);
            if (inSlot != null && !inSlot.m_equipped)
                player.EquipItem(inSlot, triggerEquipEffects: false);
            ForgetGone(player, inSlot);
        }

        /// <summary>
        /// A pack that left the inventory still worn (crafted away by an upgrade in place, trashed, salvaged) loses the mark,
        /// and the game's UnequipItem is called on it: it takes nothing off any more, but the mods that follow the equipment
        /// hear of it (Epic Loot drops the pack's effects).
        /// </summary>
        private static void ForgetGone(Player player, ItemDrop.ItemData inSlot)
        {
            ItemDrop.ItemData gone = lastWorn;
            lastWorn = inSlot;
            if (gone == null || gone == inSlot || !gone.m_equipped || player.GetInventory().ContainsItem(gone))
                return;
            gone.m_equipped = false;
            player.UnequipItem(gone, triggerEquipEffects: false);
        }

        /// <summary>The flag goes even where the game's UnequipItem did not count the pack as worn (not the local player).</summary>
        private static void TakeOffOthers(Humanoid humanoid, ItemDrop.ItemData keep)
        {
            List<ItemDrop.ItemData> items = humanoid.GetInventory().GetAllItems();
            for (int i = items.Count - 1; i >= 0; i--)
            {
                ItemDrop.ItemData item = items[i];
                if (item == keep || !item.m_equipped || BackpackCatalog.Of(item) == null)
                    continue;
                humanoid.UnequipItem(item, triggerEquipEffects: false);
                item.m_equipped = false;
            }
        }

        private static bool CanWear(Humanoid humanoid, ItemDrop.ItemData item) =>
            InventoryState.IsLocal(humanoid) && InventoryState.Active && BackpackSettings.Active
            && InventoryState.CellsOf(SlotKind.Backpack).Count > 0 && humanoid.GetInventory().ContainsItem(item);
    }
}
