using System;
using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// What an item is alike by, before anything is learned: the groups of Burrow.Stow.yml it belongs to, a theme
    /// from the game's item type for everything that is not a plain material (food, trophies, armour, weapons and tools,
    /// ammo, utility items, fish) and its smelting family (<see cref="TidyFamilies"/>). Two prefabs sharing a label are
    /// alike. A chest's memory names prefabs that may not be in any inventory any more, so the shared data comes from
    /// the object database. Labels are kept per prefab until the stow rules or the object database change.
    /// </summary>
    internal static class TidyLabels
    {
        private static readonly string[] None = new string[0];
        private static readonly Dictionary<string, string[]> labels = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private static ItemGroups keptForGroups;
        private static ObjectDB keptForDatabase;

        public static string[] Of(string prefab, ItemDrop.ItemData.SharedData shared = null)
        {
            Reset();
            if (labels.TryGetValue(prefab, out string[] found))
                return found;
            shared = shared ?? SharedOf(prefab);
            List<string> list = new List<string>();
            foreach (string group in StowRules.Groups.Names)
            {
                if (StowRules.Groups.Matches(group, prefab, shared))
                    list.Add("group " + group);
            }
            AddIfAny(list, TypeTheme(shared));
            AddIfAny(list, TidyFamilies.Of(prefab));
            found = list.Count == 0 ? None : list.ToArray();
            if (shared != null)
                labels[prefab] = found;
            return found;
        }

        /// <summary>The two prefabs share a label.</summary>
        public static bool Shared(string a, string b)
        {
            string[] first = Of(a);
            if (first.Length == 0)
                return false;
            string[] second = Of(b);
            foreach (string label in first)
            {
                if (Array.IndexOf(second, label) >= 0)
                    return true;
            }
            return false;
        }

        private static void Reset()
        {
            if (ReferenceEquals(keptForGroups, StowRules.Groups) && ReferenceEquals(keptForDatabase, ObjectDB.instance))
                return;
            labels.Clear();
            keptForGroups = StowRules.Groups;
            keptForDatabase = ObjectDB.instance;
        }

        private static void AddIfAny(List<string> list, string label)
        {
            if (label != null)
                list.Add(label);
        }

        private static string TypeTheme(ItemDrop.ItemData.SharedData shared)
        {
            if (shared == null)
                return null;
            switch (shared.m_itemType)
            {
                case ItemDrop.ItemData.ItemType.Consumable: return "food";
                case ItemDrop.ItemData.ItemType.Trophy: return "trophies";
                case ItemDrop.ItemData.ItemType.Fish: return "fish";
                case ItemDrop.ItemData.ItemType.Ammo:
                case ItemDrop.ItemData.ItemType.AmmoNonEquipable: return "ammo";
                case ItemDrop.ItemData.ItemType.Helmet:
                case ItemDrop.ItemData.ItemType.Chest:
                case ItemDrop.ItemData.ItemType.Legs:
                case ItemDrop.ItemData.ItemType.Shoulder:
                case ItemDrop.ItemData.ItemType.Hands: return "armour";
                case ItemDrop.ItemData.ItemType.Utility:
                case ItemDrop.ItemData.ItemType.Trinket: return "utility";
                default: return GearTheme(shared.m_itemType);
            }
        }

        private static string GearTheme(ItemDrop.ItemData.ItemType type)
        {
            switch (type)
            {
                case ItemDrop.ItemData.ItemType.OneHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                case ItemDrop.ItemData.ItemType.Bow:
                case ItemDrop.ItemData.ItemType.Shield:
                case ItemDrop.ItemData.ItemType.Torch:
                case ItemDrop.ItemData.ItemType.Tool:
                case ItemDrop.ItemData.ItemType.Attach_Atgeir: return "weapons and tools";
                default: return null;
            }
        }

        private static ItemDrop.ItemData.SharedData SharedOf(string prefab)
        {
            GameObject item = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop drop = item != null ? item.GetComponent<ItemDrop>() : null;
            return drop != null && drop.m_itemData != null ? drop.m_itemData.m_shared : null;
        }
    }
}
