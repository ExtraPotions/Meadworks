// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Sockets;
using ExtraPotions.Inscribe.Adopted.Stones;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The Sockets tab (sockets.md section 6, shown while <c>Gems and sockets</c> is on): the gear you carry that takes
    /// sockets on the left; on the right the chosen piece with its sockets and gems, the Dvergr Chisel and the gems you
    /// carry (the rune row), the item's sockets (the essence row: pick a filled one to replace its gem), the cost and the
    /// button. A press runs the same checks and commit as clicking the stone onto the item, paid from the table's own stones
    /// first, then your inventory (stored and taken out like runes: Store all, Ctrl + click, Shift + click); a gem
    /// aimed at a filled socket asks first (<see cref="GemChooser.Confirm"/>), the old gem being lost.
    /// </summary>
    internal sealed class SocketTab : TableTab
    {
        private readonly List<ItemDrop.ItemData> _gear = new List<ItemDrop.ItemData>();
        private readonly List<ListRow> _rows = new List<ListRow>();
        private readonly List<string> _stones = new List<string>();
        private ItemDrop.ItemData? _item;
        private string _stone = StoneCatalog.ChiselId;
        private int _socket = -1;

        public override string Label => "$ecf_table_tab_sockets";

        public override bool Shown => SocketSwitch.On;

        public override void Fill(TableView view)
        {
            Gather(view.Inventory);
            if (_item == null || !_gear.Contains(_item))
            {
                _item = _gear.Count > 0 ? _gear[0] : null;
                _socket = -1;
            }
            _rows.Clear();
            foreach (ItemDrop.ItemData item in _gear)
            {
                ItemDrop.ItemData chosen = item;
                _rows.Add(new ListRow(item.GetIcon(), InscribePane.NameOf(item), item.m_quality.ToString(), null,
                    item == _item, dim: false, () => Choose(chosen), item));
            }
            view.Parts.List?.Fill(_rows);
            SocketPane.Stones(view.Store, view.Inventory, view.Parts.Runes?.Count ?? 0, _stones);
            _stone = _stones.Contains(_stone) ? _stone : _stones[0];
            SocketPane.Fill(view, new SocketChoice(_item, _stone, Filled(_socket)), _stones);
        }

        public override void PickRune(int index)
        {
            if (index < _stones.Count)
            {
                _stone = _stones[index];
                if (TableWindow.ShiftHeld)
                {
                    TakeOut(_stone);
                }
                TableWindow.MarkDirty();
            }
        }

        /// <summary>Store all, as on the Inscribe tab: every rune, chisel, gem and Essence you carry.</summary>
        public override void Extra(TableView view) => StoreAll(view);

        public override void PickEssence(int index)
        {
            _socket = _socket == index ? -1 : index;
            TableWindow.MarkDirty();
        }

        public override void Act(TableView view, bool all)
        {
            ItemDrop.ItemData? stone = TableIcons.RuneItem(_stone);
            if (_item == null || stone == null)
            {
                return;
            }
            StoneJob job = StoneJob.Create(view.Player, stone, _item, new TableSupply(view.Store, null));
            int socket = Filled(_socket);
            if (StoneCatalog.IsGem(_stone) && socket >= 0)
            {
                GemChooser.Confirm(job, socket);
                return;
            }
            Run(view, job, all);
        }

        // Every check, then the socket pick (a gem onto a full item) or the commit.
        private static void Run(TableView view, StoneJob job, bool all)
        {
            StoneResult result = StonePipeline.Evaluate(job);
            if (result.Refused)
            {
                StoneFeedback.Show(view.Player, result.Refusal!);
                return;
            }
            if (result.NeedsSocket)
            {
                GemChooser.Ask(job);
                return;
            }
            ConfirmGate.Pass(job, result, all);
        }

        // The picked socket when it holds something (a gem goes there, replacing it); -1 for none or an empty one.
        private int Filled(int socket) =>
            _item != null && socket >= 0 && socket < ItemState.Read(_item).FilledSockets ? socket : -1;

        private void Choose(ItemDrop.ItemData item)
        {
            _item = item;
            _socket = -1;
            TableWindow.MarkDirty();
        }

        // Gear that takes sockets, in the inventory's own order: top row first, left to right.
        private void Gather(Inventory inventory)
        {
            _gear.Clear();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (ItemClasses.IsMagicBase(item) && GemCatalog.BaseOf(ItemClasses.Classify(item)) != SocketBase.None)
                {
                    _gear.Add(item);
                }
            }
            _gear.Sort((a, b) => a.m_gridPos.y != b.m_gridPos.y ? a.m_gridPos.y.CompareTo(b.m_gridPos.y) : a.m_gridPos.x.CompareTo(b.m_gridPos.x));
        }
    }

    /// <summary>What the Sockets tab has chosen: the gear, the stone (chisel or gem), the filled socket to replace (-1 none).</summary>
    internal readonly struct SocketChoice
    {
        public SocketChoice(ItemDrop.ItemData? item, string stone, int socket)
        {
            Item = item;
            Stone = stone;
            Socket = socket;
        }

        public ItemDrop.ItemData? Item { get; }
        public string Stone { get; }
        public int Socket { get; }

        public StoneDef? Def => TableIcons.Def(Stone);
    }
}
