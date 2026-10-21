// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Text;
using ExtraPotions.Inscribe.Runtime;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The Sacrifice tab (rune-table.md section 3): the trophies you carry that the table takes, each with its count; on
    /// the right what one is worth and the table's pool of pure essence. Sacrifice gives up one, Shift every one of that kind,
    /// and banks their essence in the table; Sacrifice all trophies (under the name) gives up every trophy it takes.
    /// </summary>
    internal sealed class SacrificeTab : TableTab
    {
        private readonly List<(string Prefab, ItemDrop.ItemData First, int Count)> _trophies = new List<(string, ItemDrop.ItemData, int)>();
        private readonly List<ListRow> _rows = new List<ListRow>();
        private string? _chosen;

        public override string Label => "$ecf_table_tab_sacrifice";

        public override void Fill(TableView view)
        {
            Gather(view.Inventory);
            if (_chosen == null || _trophies.FindIndex(t => t.Prefab == _chosen) < 0)
            {
                _chosen = _trophies.Count > 0 ? _trophies[0].Prefab : null;
            }
            _rows.Clear();
            foreach ((string prefab, ItemDrop.ItemData first, int count) in _trophies)
            {
                string chosen = prefab;
                _rows.Add(new ListRow(first.GetIcon(), Words.Localize(first.m_shared.m_name), count.ToString(), null,
                    prefab == _chosen, dim: false, () => Choose(chosen)));
            }
            view.Parts.List?.Fill(_rows);
            FillPane(view, _trophies.Find(t => t.Prefab == _chosen));
        }

        public override void Act(TableView view, bool all)
        {
            (string prefab, ItemDrop.ItemData first, int count) = _trophies.Find(t => t.Prefab == _chosen);
            if (first == null || !TrophyYields.TryGet(first, out int each) || !view.Store.Writable)
            {
                return;
            }
            int given = Take(view.Inventory, prefab, all ? count : 1);
            if (given <= 0)
            {
                return;
            }
            view.Store.AddEssence(given * each);
            Report(view, given * each);
        }

        /// <summary>Sacrifice all trophies: every trophy the table takes but the bosses', out of the whole inventory, in one press.</summary>
        public override void Extra(TableView view)
        {
            Gather(view.Inventory);
            if (!AnyForAll() || !view.Store.Writable)
            {
                return;
            }
            int gained = 0;
            foreach ((string prefab, ItemDrop.ItemData first, int count) in _trophies)
            {
                if (!TrophyYields.IsBoss(first) && TrophyYields.TryGet(first, out int each))
                {
                    gained += Take(view.Inventory, prefab, count) * each;
                }
            }
            if (gained <= 0)
            {
                return;
            }
            view.Store.AddEssence(gained);
            Report(view, gained);
        }

        // One top-left line with the essence gained, and the game's crafting sound.
        private static void Report(TableView view, int gained)
        {
            view.Player.Message(MessageHud.MessageType.TopLeft, Words.Localize("$ecf_table_sacrificed", gained.ToString()));
            InventoryGui.instance?.m_craftItemEffects.Create(view.Player.transform.position, Quaternion.identity);
        }

        private void Choose(string prefab)
        {
            _chosen = prefab;
            TableWindow.MarkDirty();
        }

        private void FillPane(TableView view, (string Prefab, ItemDrop.ItemData First, int Count) trophy)
        {
            PanelParts parts = view.Parts;
            bool has = trophy.First != null && TrophyYields.TryGet(trophy.First, out _);
            string name = has ? Words.Localize(trophy.First!.m_shared.m_name) : Words.Localize("$ecf_table_no_trophies");
            PaneText.Header(parts, has ? trophy.First!.GetIcon() : null, name);
            PaneText.Body(parts, Describe(view, trophy.First, trophy.Count));
            PaneText.Labels(parts, null, null);
            parts.Runes?.Show(false);
            parts.Essences?.Show(false);
            parts.Cost?.Clear();
            if (has)
            {
                parts.Cost?.Add(trophy.First!.GetIcon(), name, 1, true);
            }
            PaneText.Button(parts, "$ecf_table_sacrifice", has && view.Store.Writable);
            PaneText.Extra(parts, "$ecf_table_sacrifice_all", AnyForAll() && view.Store.Writable);
        }

        // Whether Sacrifice all trophies has anything to take: a trophy that is not a boss's.
        private bool AnyForAll() => _trophies.Exists(t => !TrophyYields.IsBoss(t.First));

        private static string Describe(TableView view, ItemDrop.ItemData? trophy, int count)
        {
            var sb = new StringBuilder();
            if (trophy != null && TrophyYields.TryGet(trophy, out int each))
            {
                sb.Append(Words.Localize("$ecf_table_trophy_worth", each.ToString()));
                sb.Append('\n').Append(Words.Localize("$ecf_table_trophy_all", count.ToString(), (count * each).ToString()));
                if (TrophyYields.IsBoss(trophy))
                {
                    sb.Append('\n').Append(Words.Localize("$ecf_table_trophy_boss"));
                }
            }
            else
            {
                sb.Append(Words.Localize("$ecf_table_no_trophies_desc"));
            }
            sb.Append("\n\n").Append(Words.Localize("$ecf_table_pool", view.Store.Essence.ToString()));
            return sb.ToString();
        }

        // The trophies the table takes, one entry per kind, in the order they lie in the inventory.
        private void Gather(Inventory inventory)
        {
            _trophies.Clear();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                string? prefab = ItemTier.PrefabName(item);
                if (prefab == null || !TrophyYields.TryGet(item, out _))
                {
                    continue;
                }
                int at = _trophies.FindIndex(t => t.Prefab == prefab);
                if (at < 0)
                {
                    _trophies.Add((prefab, item, item.m_stack));
                }
                else
                {
                    _trophies[at] = (prefab, _trophies[at].First, _trophies[at].Count + item.m_stack);
                }
            }
        }

        private static int Take(Inventory inventory, string prefab, int count)
        {
            int taken = 0;
            List<ItemDrop.ItemData> items = inventory.GetAllItems();
            for (int i = items.Count - 1; i >= 0 && taken < count; i--)
            {
                ItemDrop.ItemData item = items[i];
                if (ItemTier.PrefabName(item) != prefab)
                {
                    continue;
                }
                int take = Mathf.Min(item.m_stack, count - taken);
                taken += SafeInventory.RemoveFrom(inventory, item, take);
            }
            return taken;
        }
    }
}
