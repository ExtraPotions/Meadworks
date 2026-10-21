// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Tables
{
    /// <summary>
    /// The Rune Table paying for one use (rune-table.md section 4): the rune, chisel or gem from the table's store, and,
    /// when an essence was chosen, its cost from the table's one pool. Only what the table holds (user 2026-10-07: "the
    /// chisel must be in the table to use it with the cut socket button"); stones you carry are used the normal way, clicked
    /// onto an item. Made per press; pays on the client that holds the table open (its ZDO owner).
    /// </summary>
    internal sealed class TableSupply : IRuneSupply
    {
        private readonly TableStore _store;
        private readonly Essence? _essence;
        private string? _paidId;
        private int _paidRunes;
        private int _paidEssence;

        public TableSupply(TableStore store, Essence? essence)
        {
            _store = store;
            _essence = essence;
        }

        public ISet<string>? Favoured => _essence?.Inscriptions;


        public int Held(string? runeId) => runeId == null ? 0 : _store.Runes(runeId);

        /// <summary>Essence this use can pay from: the table's pool.</summary>
        public int EssenceHeld() => _store.Essence;

        public StoneMessage? Shortfall(StoneJob job)
        {
            if (!_store.Writable)
            {
                return new StoneMessage("table_lost", new string[0]);
            }
            if (_essence == null)
            {
                return null;
            }
            int cost = Essences.CostFor(ItemTier.Of(job.Target));
            return EssenceHeld() >= cost ? null : new StoneMessage("not_enough_essence", new[] { cost.ToString() });
        }

        public bool Pay(StoneJob job)
        {
            _paidId = null;
            _paidRunes = 0;
            _paidEssence = 0;
            string? runeId = job.Def?.Id;
            if (runeId != null && job.Cost > 0)
            {
                if (!_store.TakeRunes(runeId, job.Cost))
                {
                    return false;
                }
                _paidId = runeId;
                _paidRunes = job.Cost;
            }
            if (_essence != null)
            {
                int cost = Essences.CostFor(ItemTier.Of(job.Target));
                if (!_store.TakeEssence(cost))
                {
                    Refund(job);
                    return false;
                }
                _paidEssence = cost;
            }
            return true;
        }

        public void Refund(StoneJob job)
        {
            if (_paidId != null && _paidRunes > 0)
            {
                _store.AddRunes(_paidId, _paidRunes);
            }
            if (_paidEssence > 0)
            {
                _store.AddEssence(_paidEssence);
            }
            _paidId = null;
            _paidRunes = 0;
            _paidEssence = 0;
        }

        public void NoteUsed(StoneJob job)
        {
            string? runeId = job.Def?.Id;
            if (runeId != null)
            {
                _store.MarkCast(runeId);
            }
        }
    }

    /// <summary>Runes (by rune id) and pure essence (by prefab) in an inventory, counted and taken wherever they lie in it.</summary>
    internal static class RuneBag
    {
        public static int Count(Inventory inventory, string runeId) => CountPrefab(inventory, StoneCatalog.PrefabFor(runeId));

        public static void Remove(Inventory inventory, string runeId, int count) =>
            RemovePrefab(inventory, StoneCatalog.PrefabFor(runeId), count);

        public static int CountPrefab(Inventory inventory, string prefab)
        {
            int count = 0;
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (ItemTier.PrefabName(item) == prefab)
                {
                    count += item.m_stack;
                }
            }
            return count;
        }

        public static void RemovePrefab(Inventory inventory, string prefab, int count)
        {
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            for (int i = items.Count - 1; i >= 0 && count > 0; i--)
            {
                ItemDrop.ItemData item = items[i];
                if (ItemTier.PrefabName(item) != prefab)
                {
                    continue;
                }
                int take = System.Math.Min(item.m_stack, count);
                inventory.RemoveItem(item, take);
                count -= take;
            }
        }
    }
}
