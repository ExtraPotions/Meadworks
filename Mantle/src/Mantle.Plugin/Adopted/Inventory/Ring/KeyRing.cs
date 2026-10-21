using System.Collections.Generic;
using System;
using Mantle.Core;
using Mantle.Layout;
using Mantle.Slots;
using UnityEngine;

namespace Mantle.Ring
{
    /// <summary>
    /// The keys the ring holds: the prefab names of Key Items in their order, each with its own ring cell (slot key1 holds
    /// the first key, key2 the second...). An item is a ring key by its prefab name. The ring cells are ordinary cells of
    /// the player's inventory, so a door, which checks the whole inventory for its key, finds a key on the ring.
    /// </summary>
    public static class KeyRing
    {
        private static string parsed;
        private static List<string> prefabs = new List<string>();
        private static string[] sharedNames;

        /// <summary>The ring is part of the layout the settings ask for: the master switch and Key Ring on.</summary>
        public static bool On => InventorySettings.Enabled.Value && KeyRingSettings.KeyRing.Value;

        /// <summary>The layout in use has ring cells.</summary>
        public static bool Active => InventoryState.Active && InventoryState.CellsOf(SlotKind.Key).Count > 0;

        public static IReadOnlyList<string> Prefabs
        {
            get
            {
                Parse();
                return prefabs;
            }
        }

        /// <summary>The ring number (1 for the first key) of a prefab, or 0 when it is not a ring key.</summary>
        public static int NumberOf(string prefab)
        {
            IReadOnlyList<string> list = Prefabs;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], prefab, StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
            return 0;
        }

        public static int NumberOf(ItemDrop.ItemData item) => item != null ? NumberOf(ItemNames.PrefabName(item)) : 0;

        public static bool IsKey(ItemDrop.ItemData item) => NumberOf(item) > 0;

        /// <summary>The ring cell of a key in a layout, or (-1, -1) for an item that is not a ring key or a layout without the ring.</summary>
        public static Vector2i CellOf(InventoryLayout layout, ItemDrop.ItemData item)
        {
            int number = NumberOf(item);
            IReadOnlyList<Vector2i> cells = layout != null ? layout.CellsOf(SlotKind.Key) : null;
            return number > 0 && cells != null && number <= cells.Count ? cells[number - 1] : new Vector2i(-1, -1);
        }

        /// <summary>The ring cell of a key in the layout in use.</summary>
        public static Vector2i CellOf(ItemDrop.ItemData item) => InventoryState.Active ? CellOf(InventoryState.Layout, item) : new Vector2i(-1, -1);

        /// <summary>The key of ring number <paramref name="number"/> as the item database has it, or null.</summary>
        public static ItemDrop.ItemData Key(int number)
        {
            IReadOnlyList<string> list = Prefabs;
            GameObject prefab = number >= 1 && number <= list.Count && ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(list[number - 1]) : null;
            ItemDrop drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            return drop != null ? drop.m_itemData : null;
        }

        /// <summary>
        /// How many of each ring key the whole inventory holds, every world level together, by ring number (index 0 is
        /// unused). Matched by shared name, which needs no string made per item, as this runs every frame the ring shows.
        /// </summary>
        public static int[] Counts(Inventory inventory) => Counts(inventory, null);

        /// <summary>The same, written into <paramref name="reuse"/> when it has the right length, so the per-frame caller makes no array.</summary>
        public static int[] Counts(Inventory inventory, int[] reuse)
        {
            int[] counts = reuse != null && reuse.Length == Prefabs.Count + 1 ? reuse : new int[Prefabs.Count + 1];
            System.Array.Clear(counts, 0, counts.Length);
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                for (int number = 1; number < counts.Length; number++)
                {
                    if (item.m_shared.m_name == SharedName(number))
                        counts[number] += item.m_stack;
                }
            }
            return counts;
        }

        /// <summary>Whether two count arrays hold the same numbers (null matches nothing).</summary>
        public static bool SameCounts(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }

        /// <summary>The shared name of key <paramref name="number"/>, kept once the item database has it.</summary>
        private static string SharedName(int number)
        {
            if (sharedNames == null || sharedNames.Length != Prefabs.Count)
                sharedNames = new string[Prefabs.Count];
            if (sharedNames[number - 1] == null)
                sharedNames[number - 1] = Key(number)?.m_shared.m_name;
            return sharedNames[number - 1];
        }

        private static void Parse()
        {
            string value = KeyRingSettings.KeyItems.Value ?? "";
            if (value == parsed)
                return;
            sharedNames = null;
            List<string> list = new List<string>();
            foreach (string part in value.Split(','))
            {
                string name = part.Trim();
                if (name.Length > 0 && !list.Exists(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase)))
                    list.Add(name);
            }
            prefabs = list;
            parsed = value;
        }
    }
}
