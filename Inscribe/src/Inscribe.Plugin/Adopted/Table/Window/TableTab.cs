// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>What a tab fills and does in the Rune Table's window: one each for Inscribe, Sacrifice and Sockets.</summary>
    internal abstract class TableTab
    {
        /// <summary>The tab button's word.</summary>
        public abstract string Label { get; }

        /// <summary>Whether the tab's button shows (the Sockets tab only while <c>Gems and sockets</c> is on).</summary>
        public virtual bool Shown => true;

        /// <summary>Fills the list and the description panel for the table and player now.</summary>
        public abstract void Fill(TableView view);

        /// <summary>The main button; <paramref name="all"/> is Shift held.</summary>
        public abstract void Act(TableView view, bool all);

        /// <summary>The small button under the name, where the tab shows it.</summary>
        public virtual void Extra(TableView view)
        {
        }

        /// <summary>A click on the rune row's slot.</summary>
        public virtual void PickRune(int index)
        {
        }

        /// <summary>A click on the essence row's slot.</summary>
        public virtual void PickEssence(int index)
        {
        }

        /// <summary>Store all: every rune, chisel, gem and Essence the player carries goes into the table.</summary>
        protected static void StoreAll(TableView view)
        {
            int stored = RuneStash.StoreAll(view.Store, view.Inventory);
            if (stored > 0)
            {
                view.Player.Message(MessageHud.MessageType.TopLeft, Text.Words.Localize("$ecf_table_stored", stored.ToString()));
            }
        }

        /// <summary>Shift + click on a stone of the row: how many, in the game's split dialog, then those into the inventory.</summary>
        protected static void TakeOut(string id)
        {
            TableWindow.Run(view =>
            {
                ItemDrop.ItemData? item = TableIcons.RuneItem(id);
                TableSplit.Ask(item?.GetIcon(), Text.Words.Localize(item?.m_shared.m_name ?? id), view.Store.Runes(id),
                    amount => TableWindow.Run(v => Took(v, RuneStash.TakeOut(v.Store, v.Inventory, id, amount))));
            });
        }

        /// <summary>Shift + click on the Essence slot: how many, then that much of the pool as Essence items.</summary>
        protected static void TakeEssence()
        {
            TableWindow.Run(view =>
            {
                Sprite? icon = TableIcons.EssenceItem();
                TableSplit.Ask(icon, Text.Words.Localize("$ecf_essence_item"), view.Store.Essence,
                    amount => TableWindow.Run(v => Took(v, RuneStash.TakeEssence(v.Store, v.Inventory, amount))));
            });
        }

        private static void Took(TableView view, int moved)
        {
            string key = moved > 0 ? "$ecf_table_taken" : "$ecf_table_take_noroom";
            view.Player.Message(MessageHud.MessageType.TopLeft, Text.Words.Localize(key, moved.ToString()));
        }
    }

    /// <summary>What a tab works on: the open table, the local player, the window's parts.</summary>
    internal sealed class TableView
    {
        public TableView(RuneTable table, Player player, PanelParts parts)
        {
            Table = table;
            Player = player;
            Parts = parts;
        }

        public RuneTable Table { get; }
        public Player Player { get; }
        public PanelParts Parts { get; }

        public TableStore Store => Table.Store;
        public Inventory Inventory => Player.GetInventory();
    }
}
