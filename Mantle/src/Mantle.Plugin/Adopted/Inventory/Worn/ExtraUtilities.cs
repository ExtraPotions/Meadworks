using System.Collections.Generic;
using Mantle.Accessories;
using Mantle.Core;
using Mantle.Slots;
using UnityEngine;

namespace Mantle.Worn
{
    /// <summary>
    /// Up to five utility items worn at once, one per Utility slot. The game has one utility field
    /// (<c>Humanoid.m_utilityItem</c>); the others are kept here, on the local player only, and every game rule that reads the utility field is given
    /// them too (<see cref="UtilityEffectPatches"/>): equip effects, set bonuses, eitr regen, the equipment modifiers
    /// (movement, heat resistance, ...), weight and durability. Only the first shows on the character. A second item
    /// of a name already worn is not worn twice (two belts would give one effect); equipping it swaps with the
    /// game's utility as before. Status effects run on the player's own client, so nothing is sent. The game belt in the belt
    /// cell (<see cref="Belt"/>, worn by <see cref="BeltWear"/>) is kept in the same list so every rule below reaches it, but it is
    /// not one of the utility items: it takes none of the five utility slots.
    /// </summary>
    public static class ExtraUtilities
    {
        private static readonly List<ItemDrop.ItemData> worn = new List<ItemDrop.ItemData>();

        private static ItemDrop.ItemData belt;

        public static IReadOnlyList<ItemDrop.ItemData> Worn => worn;

        /// <summary>The game belt worn from the belt cell, or null.</summary>
        public static ItemDrop.ItemData Belt => belt != null && worn.Contains(belt) ? belt : null;

        public static bool IsWorn(ItemDrop.ItemData item) => item != null && worn.Contains(item);

        /// <summary>A new character is loading: nothing is worn until the game equips its saved items.</summary>
        public static void Reset()
        {
            worn.Clear();
            belt = null;
            ExtraEffects.Reset();
        }

        /// <summary>True when equipping <paramref name="item"/> should wear it beside the game's utility.</summary>
        public static bool Takes(Humanoid humanoid, ItemDrop.ItemData item)
        {
            if (!InventoryState.IsLocal(humanoid) || !InventoryState.Active || item == null)
                return false;
            if (AccessoryBonus.Claims(item))
                return false;   // a belt or ring (the Megingjord) belongs to its own slot, never a utility one
            if (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility || worn.Contains(item))
                return false;
            ItemDrop.ItemData first = humanoid.m_utilityItem;
            if (first == null || first == item || 1 + worn.Count - (Belt != null ? 1 : 0) >= SlotCounts.Utility)
                return false;
            return !NameWorn(first, item);
        }

        /// <summary>Wears the item with the game's own guards, effects and refresh. False when a guard refuses.</summary>
        public static bool Wear(Humanoid humanoid, ItemDrop.ItemData item, bool triggerEquipEffects) =>
            Wear(humanoid, item, triggerEquipEffects, asBelt: false, effects: true);

        /// <summary>
        /// Wears the game belt lying in the belt cell. It stays quiet when a guard refuses (it is tried again next frame) and plays
        /// the equip effect only when asked (a right click, not a load or a respawn).
        /// </summary>
        public static bool WearBelt(Humanoid humanoid, ItemDrop.ItemData item, bool equipEffects)
        {
            return Wear(humanoid, item, equipEffects, asBelt: true, effects: equipEffects);
        }

        private static bool Wear(Humanoid humanoid, ItemDrop.ItemData item, bool triggerEquipEffects, bool asBelt, bool effects)
        {
            if (!CanWear(humanoid, item, quiet: asBelt))
                return false;
            worn.Add(item);
            if (asBelt)
                belt = item;
            item.m_equipped = true;
            if (effects)
            {
                Transform transform = humanoid.transform;
                item.m_shared.m_equipEffect.Create(transform.position + Vector3.up, transform.rotation, null, 1f, -1, humanoid.GetZDOID());
            }
            humanoid.SetupEquipment();
            if (triggerEquipEffects)
                humanoid.TriggerEquipEffect(item);
            return true;
        }

        /// <summary>After the game's UnequipItem ran on an item: it is no longer worn.</summary>
        public static void Forget(ItemDrop.ItemData item)
        {
            worn.Remove(item);
            if (belt == item)
                belt = null;
        }

        /// <summary>Takes every extra utility off through the game's UnequipItem (death, unequip all, fewer slots).</summary>
        public static void TakeOffAll(Humanoid humanoid)
        {
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(worn))
                humanoid.UnequipItem(item, triggerEquipEffects: false);
            worn.Clear();
            belt = null;
        }

        /// <summary>
        /// Forgets items that left the inventory or lost their equipped flag without the game's UnequipItem (a trashed or
        /// salvaged stack), and refreshes the equipment so their effects go. Every frame; the list holds four at most.
        /// </summary>
        public static void Prune(Humanoid humanoid)
        {
            Inventory inventory = humanoid.GetInventory();
            if (worn.RemoveAll(item => !item.m_equipped || !inventory.ContainsItem(item)) > 0)
            {
                humanoid.SetupEquipment();
                if (belt != null && !worn.Contains(belt))
                    belt = null;
            }
        }

        private static bool NameWorn(ItemDrop.ItemData first, ItemDrop.ItemData item)
        {
            if (first.m_shared.m_name == item.m_shared.m_name)
                return true;
            return worn.Exists(other => other.m_shared.m_name == item.m_shared.m_name);
        }

        /// <summary>The checks of the game's EquipItem that apply to a utility item.</summary>
        private static bool CanWear(Humanoid humanoid, ItemDrop.ItemData item, bool quiet)
        {
            if (!humanoid.GetInventory().ContainsItem(item) || humanoid.InAttack() || humanoid.InDodge())
                return false;
            if (humanoid.IsSwimming() && !humanoid.IsOnGround() && !humanoid.IsDead())
                return false;
            if (item.m_shared.m_useDurability && item.m_durability <= 0f)
                return false;
            if (Game.m_worldLevel > 0 && item.m_worldLevel < Game.m_worldLevel)
            {
                if (!quiet)
                    humanoid.Message(MessageHud.MessageType.Center, "$msg_ng_item_too_low");
                return false;
            }
            return true;
        }
    }
}
