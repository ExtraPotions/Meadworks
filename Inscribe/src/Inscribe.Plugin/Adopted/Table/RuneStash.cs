// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Runtime;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// Runes, the Dvergr Chisel, gems and pure essence going into a Rune Table and out of it again (rune-table.md section 5;
    /// the socket stones the same way since 2026-10-07, user: "socket things need to have the same concept as runes"). In: Store all moves
    /// every rune and every pure essence the player carries into the table (the Inscribe tab's small button), and Ctrl +
    /// click puts one stack in (<see cref="StoreStack"/>). Out: Shift + click on a stone of a row takes as many of that
    /// kind as the player picks in the game's split dialog and as fit (<see cref="TakeOut"/>; user 2026-10-07), and on the
    /// Essence slot as much of the pool, as Essence items (<see cref="TakeEssence"/>).
    /// When the table is destroyed or taken down with the hammer, its ZDO owner drops every rune it holds and its pool as
    /// pure essence items where it stood, as a chest drops its contents.
    /// The table is credited only with what actually left the inventory: another mod can refuse the removal (Burrow's
    /// inventory hold), and crediting regardless would copy the stack (playtest 2026-10-20).
    /// </summary>
    internal static class RuneStash
    {
        /// <summary>Moves every rune and pure essence in the inventory into the table; the count moved (0 without ownership).</summary>
        public static int StoreAll(TableStore store, Inventory inventory)
        {
            if (!store.Writable)
            {
                return 0;
            }
            int stored = 0;
            foreach (string id in StoneCatalog.AllIds)
            {
                int carried = RuneBag.Count(inventory, id);
                if (carried <= 0) continue;
                int moved = SafeInventory.Removed(() => RuneBag.Count(inventory, id), () => RuneBag.Remove(inventory, id, carried));
                if (moved <= 0) continue;
                store.AddRunes(id, moved);
                stored += moved;
            }
            int essence = RuneBag.CountPrefab(inventory, EssenceItem.PrefabName);
            int essenceMoved = essence <= 0 ? 0
                : SafeInventory.Removed(() => RuneBag.CountPrefab(inventory, EssenceItem.PrefabName), () => RuneBag.RemovePrefab(inventory, EssenceItem.PrefabName, essence));
            if (essenceMoved > 0) store.AddEssence(essenceMoved);
            return stored + essenceMoved;
        }

        /// <summary>One stack the player Ctrl + clicked: a rune or Essence into the table; the count moved, 0 for anything else.</summary>
        public static int StoreStack(TableStore store, Inventory inventory, ItemDrop.ItemData item)
        {
            string? runeId = StoneIdOf(item);
            bool essence = EssenceItem.Is(item);
            if (!store.Writable || (runeId == null && !essence) || !inventory.ContainsItem(item))
            {
                return 0;
            }
            int count = item.m_stack;
            if (!SafeInventory.RemoveStack(inventory, item))
            {
                return 0;
            }
            if (runeId != null)
            {
                store.AddRunes(runeId, count);
            }
            else
            {
                store.AddEssence(count);
            }
            return count;
        }

        /// <summary>Up to <paramref name="amount"/> of the table's stones of one kind into the inventory, as many as fit; the count moved.</summary>
        public static int TakeOut(TableStore store, Inventory inventory, string runeId, int amount)
        {
            int count = store.Writable ? Mathf.Min(store.Runes(runeId), amount) : 0;
            int moved = count > 0 ? Give(inventory, StonePrefabs.Get(runeId), count) : 0;
            return moved > 0 && store.TakeRunes(runeId, moved) ? moved : 0;
        }

        /// <summary>Up to <paramref name="amount"/> of the pool into the inventory as Essence items, as many as fit; the count moved.</summary>
        public static int TakeEssence(TableStore store, Inventory inventory, int amount)
        {
            int count = store.Writable ? Mathf.Min(store.Essence, amount) : 0;
            int moved = count > 0 ? Give(inventory, EssenceItem.Prefab, count) : 0;
            return moved > 0 && store.TakeEssence(moved) ? moved : 0;
        }

        // Adds the prefab's item in full stacks: the game's add fills partial stacks first and returns false when the rest
        // found no room. Counted before and after, so what arrived is exact.
        private static int Give(Inventory inventory, GameObject? prefab, int count)
        {
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return 0;
            }
            int before = RuneBag.CountPrefab(inventory, prefab!.name);
            int max = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            int left = count;
            while (left > 0 && inventory.AddItem(Stack(drop, prefab, Mathf.Min(left, max))))
            {
                left -= max;
            }
            return RuneBag.CountPrefab(inventory, prefab.name) - before;
        }

        /// <summary>The id of a rune, the Dvergr Chisel or a gem, or null for any other item.</summary>
        public static string? StoneIdOf(ItemDrop.ItemData? item)
        {
            string? prefab = item?.m_dropPrefab != null ? item.m_dropPrefab.name : null;
            foreach (string id in StoneCatalog.AllIds)
            {
                if (prefab == StoneCatalog.PrefabFor(id))
                {
                    return id;
                }
            }
            return null;
        }

        public static int Carried(Inventory inventory)
        {
            int count = 0;
            foreach (string id in StoneCatalog.AllIds)
            {
                count += RuneBag.Count(inventory, id);
            }
            return count + RuneBag.CountPrefab(inventory, EssenceItem.PrefabName);
        }

        /// <summary>On the table's owner as it is destroyed: every rune it holds dropped beside it, in full stacks.</summary>
        public static void DropAll(TableStore store, Vector3 at)
        {
            if (!store.Writable)
            {
                return;
            }
            store.MigrateLegacy();
            foreach (string id in StoneCatalog.AllIds)
            {
                int held = store.Runes(id);
                if (held > 0 && store.TakeRunes(id, held))
                {
                    Drop(StonePrefabs.Get(id), held, at);
                }
            }
            int essence = store.Essence;
            if (essence > 0 && store.TakeEssence(essence))
            {
                Drop(EssenceItem.Prefab, essence, at);
            }
        }

        // A stack of the prefab's item at the world level, so it stacks with the player's own.
        private static ItemDrop.ItemData Stack(ItemDrop drop, GameObject prefab, int count)
        {
            ItemDrop.ItemData item = drop.m_itemData.Clone();
            item.m_dropPrefab = prefab;
            item.m_stack = count;
            item.m_worldLevel = (byte)Game.m_worldLevel;
            return item;
        }

        private static void Drop(GameObject? prefab, int count, Vector3 at)
        {
            ItemDrop? drop = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
            if (drop == null)
            {
                return;
            }
            int max = Mathf.Max(1, drop.m_itemData.m_shared.m_maxStackSize);
            for (int left = count; left > 0; left -= max)
            {
                ItemDrop.ItemData item = Stack(drop, prefab!, Mathf.Min(left, max));
                Vector3 position = at + Vector3.up * 0.5f + Random.insideUnitSphere * 0.3f;
                ItemDrop.DropItem(item, 0, position, Quaternion.Euler(0f, Random.Range(0, 360), 0f));
            }
        }
    }
}
