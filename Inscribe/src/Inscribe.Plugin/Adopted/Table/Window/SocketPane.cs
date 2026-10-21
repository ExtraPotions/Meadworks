// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Adopted.Text;
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>The Sockets tab's right side: the chosen gear, the stone row, the socket row, the cost and the button.</summary>
    internal static class SocketPane
    {
        private static readonly Color EmptySocket = new Color(1f, 1f, 1f, 0.22f);

        public static void Fill(TableView view, SocketChoice choice, List<string> stones)
        {
            PanelParts parts = view.Parts;
            PaneText.Header(parts, choice.Item?.GetIcon(),
                choice.Item != null ? InscribePane.NameOf(choice.Item) : Words.Localize("$ecf_table_no_socket_gear"), choice.Item);
            PaneText.Body(parts, choice.Item != null ? SocketText.Describe(choice) : Words.Localize("$ecf_table_no_socket_gear_desc"));
            bool sockets = choice.Item != null && ItemState.Read(choice.Item).Sockets > 0;
            PaneText.Labels(parts, "$ecf_table_label_stone", sockets ? "$ecf_table_label_socket" : null);
            FillStones(view, choice, stones);
            FillSockets(view, choice);
            bool ready = FillCost(view, choice);
            PaneText.Button(parts, StoneCatalog.IsGem(choice.Stone) ? "$ecf_table_set_gem" : "$ecf_table_cut_socket",
                ready && TableUse.Works(view, choice.Item, choice.Stone, null));
            PaneText.Extra(parts, "$ecf_table_store_all", RuneStash.Carried(view.Inventory) > 0 && view.Store.Writable);
        }

        /// <summary>The stones the row shows: the Dvergr Chisel, then each gem the table holds or you carry (a carried one,
        /// shown dim, can be stored with Ctrl + click), as many as fit.</summary>
        public static void Stones(TableStore store, Inventory inventory, int slots, List<string> into)
        {
            into.Clear();
            into.Add(StoneCatalog.ChiselId);
            foreach (string gem in StoneCatalog.GemIds)
            {
                if (into.Count < slots && store.Runes(gem) + RuneBag.Count(inventory, gem) > 0)
                {
                    into.Add(gem);
                }
            }
        }

        private static void FillStones(TableView view, SocketChoice choice, List<string> stones)
        {
            SlotRow? row = view.Parts.Runes;
            if (row == null)
            {
                return;
            }
            row.Show(true);
            row.Fit(stones.Count);
            for (int i = 0; i < stones.Count && i < row.Count; i++)
            {
                string id = stones[i];
                int stored = view.Store.Runes(id);
                StoneDef? def = TableIcons.Def(id);
                row[i].Show(TableIcons.Rune(id), null, InscribePane.Held(stored), true,
                    dim: !StoneVerbs.IsUsable(def) || stored == 0);
                row[i].Choose(id == choice.Stone, StoneVisuals.Tint(id));
                row[i].Tooltip("", "");
            }
        }

        // One slot per socket: a gem's icon, a faint mark for an empty one, the pick in the gem's colour; the rest bare.
        private static void FillSockets(TableView view, SocketChoice choice)
        {
            SlotRow? row = view.Parts.Essences;
            ItemState state = ItemState.Read(choice.Item);
            row?.Show(state.Sockets > 0);
            if (row == null || state.Sockets == 0)
            {
                return;
            }
            for (int i = 0; i < row.Count; i++)
            {
                if (i >= state.Sockets)
                {
                    row[i].Clear();
                    continue;
                }
                string? gem = SocketText.GemAt(state, i, out _);
                row[i].Show(gem != null ? TableIcons.Rune(gem) : null, null, "", true, dim: false);
                bool chosen = i == choice.Socket;
                row[i].Choose(chosen || gem == null, chosen && gem != null ? StoneVisuals.Tint(gem) : EmptySocket);
                row[i].Tooltip("", "");
            }
        }

        // The stone, one per use, from your inventory; true when the press can be paid and the stone works here.
        private static bool FillCost(TableView view, SocketChoice choice)
        {
            CostRow? cost = view.Parts.Cost;
            cost?.Clear();
            if (choice.Item == null || cost == null)
            {
                return false;
            }
            int stored = view.Store.Runes(choice.Stone);
            cost.Add(TableIcons.Rune(choice.Stone), Words.Localize(choice.Def?.Name ?? ""), 1, stored >= 1);
            return stored >= 1 && StoneVerbs.IsUsable(choice.Def);
        }
    }
}
