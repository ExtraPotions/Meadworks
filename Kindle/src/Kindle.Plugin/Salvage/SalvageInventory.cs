// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Gerald Glasgow
// Adopted from OpenKeep 3.0.0 / ValheimModLibs at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Upstream GPL-3.0-or-later notice: THIRD_PARTY_NOTICES/OpenKeep-GPL-3.0-or-later.txt.
// Changed by expDARE, 2026: Kindle namespaces and native integration boundaries.
using System.Collections.Generic;
using UnityEngine;

namespace ExtraPotions.Kindle.Runtime.Salvage
{
    /// <summary>
    /// Fitting and adding salvage returns to an inventory. <see cref="Fits"/> is an exact simulation of the game's
    /// add rules (partial stacks of quality 1 at the current world level fill first, the rest needs empty slots)
    /// with the salvaged stack's own slot counted as free. <see cref="Add"/> adds stack by stack with the game's
    /// own AddItem and rolls back everything it added when one add fails, so nothing is ever lost.
    /// </summary>
    public static class SalvageInventory
    {
        public static bool Fits(Inventory inventory, ItemDrop.ItemData salvaged, List<SalvageReturn> returns)
        {
            Player player = Player.m_localPlayer;
            bool own = player != null && inventory == player.GetInventory();
            bool ownCell = inventory.ContainsItem(salvaged) && (!own || SalvageInterop.FreedCell(player!,salvaged));
            // GetEmptySlots under an inventory mod can count slot cells (food, ammo...) that refuse the returns.
            int freeSlots = (own ? SalvageInterop.FreeCells(player!, inventory) : inventory.GetEmptySlots()) + (ownCell ? 1 : 0);
            int slotsNeeded = 0;
            foreach (SalvageReturn entry in returns)
            {
                int rest = entry.Amount - FreeStackRoom(inventory, entry.Name, salvaged);
                if (rest > 0)
                    slotsNeeded += (rest + entry.MaxStack - 1) / entry.MaxStack;
            }
            return slotsNeeded <= freeSlots;
        }

        /// <summary>Room in partial stacks the game's AddItem would fill: same name, quality 1, current world level.</summary>
        private static int FreeStackRoom(Inventory inventory, string name, ItemDrop.ItemData except)
        {
            int room = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item == except || item.m_shared.m_name != name || item.m_quality != 1)
                    continue;
                if (item.m_worldLevel != Game.m_worldLevel || item.m_stack >= item.m_shared.m_maxStackSize)
                    continue;
                room += item.m_shared.m_maxStackSize - item.m_stack;
            }
            return room;
        }

        /// <summary>Adds every return. False when one did not fit; then everything added by this call is removed again.</summary>
        public static bool Add(Inventory inventory, List<SalvageReturn> returns)
        {
            var before = new Snapshot(inventory);
            bool complete = false;
            try
            {
            foreach (SalvageReturn entry in returns)
            {
                int count = AddOne(inventory, entry);
                if (count < entry.Amount)
                    return false;
            }
            complete = true;
            return true;
            }
            finally { if (!complete) before.Restore(inventory); }
        }

        /// <summary>Adds one material in stacks of its maximum size. Returns how many units landed in the inventory.</summary>
        private static int AddOne(Inventory inventory, SalvageReturn entry)
        {
            int added = 0;
            while (added < entry.Amount)
            {
                int chunk = Mathf.Min(entry.MaxStack, entry.Amount - added);
                ItemDrop.ItemData stack = NewStack(entry.Item, chunk);
                if (inventory.AddItem(stack))
                {
                    added += chunk;
                    continue;
                }
                // The game merges into partial stacks before it fails on the remainder; the remainder stays in the stack.
                return added + (entry.MaxStack > 1 ? chunk - stack.m_stack : 0);
            }
            return added;
        }

        /// <summary>A fresh quality 1 stack of the material at the current world level, the way the game crafts one.</summary>
        public static ItemDrop.ItemData NewStack(ItemDrop drop, int amount)
        {
            ItemDrop.ItemData data = drop.m_itemData.Clone();
            data.m_dropPrefab = drop.gameObject;
            data.m_stack = amount;
            data.m_quality = 1;
            data.m_variant = 0;
            data.m_equipped = false;
            data.m_worldLevel = (byte)Game.m_worldLevel;
            return data;
        }

        // Native AddItem may merge partially before refusing. Restore the exact pre-call handles,
        // counts and cells, rather than removing by name (which can remove a pre-existing stack).
        internal sealed class Snapshot
        {
            readonly List<ItemDrop.ItemData> items;
            readonly Dictionary<ItemDrop.ItemData, int> counts = new Dictionary<ItemDrop.ItemData, int>();
            readonly Dictionary<ItemDrop.ItemData, Vector2i> cells = new Dictionary<ItemDrop.ItemData, Vector2i>();
            internal Snapshot(Inventory inventory)
            {
                items = new List<ItemDrop.ItemData>(inventory.GetAllItems());
                foreach (var item in items) { counts[item] = item.m_stack; cells[item] = item.m_gridPos; }
            }
            internal void Restore(Inventory inventory)
            {
                var live = inventory.GetAllItems();
                live.Clear();
                foreach (var item in items) { item.m_stack = counts[item]; item.m_gridPos = cells[item]; live.Add(item); }
                inventory.Changed();
            }
        }
    }
}
