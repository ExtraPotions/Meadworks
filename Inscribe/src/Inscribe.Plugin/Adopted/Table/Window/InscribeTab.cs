// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries; the biome essences.
using System.Collections.Generic;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Items;
using ExtraPotions.Inscribe.Adopted.Rules;
using ExtraPotions.Inscribe.Adopted.Stones;
using ExtraPotions.Inscribe.Adopted.Text;

namespace ExtraPotions.Inscribe.Adopted.Tables.Window
{
    /// <summary>
    /// The Inscribe tab (rune-table.md section 4): your gear on the left, as the game's Upgrade tab lists it; on the right
    /// the chosen piece with its inscriptions, the seven runes (what the table holds plus what you carry), the eight
    /// essences (the table's pool), the cost and the Inscribe button, and Store all runes under the piece's name. A press runs the same checks, confirm and commit as
    /// clicking a rune onto the item, paid by the table (<see cref="TableSupply"/>); an essence steers only runes that roll.
    /// </summary>
    internal sealed class InscribeTab : TableTab
    {
        private readonly List<ItemDrop.ItemData> _gear = new List<ItemDrop.ItemData>();
        private readonly List<ListRow> _rows = new List<ListRow>();
        private ItemDrop.ItemData? _item;
        private string _rune = StoneCatalog.BuiltInIds[0];
        private Essence? _essence;

        public override string Label => "$ecf_table_tab_inscribe";

        public override void Fill(TableView view)
        {
            Gather(view.Inventory);
            if (_item == null || !_gear.Contains(_item))
            {
                _item = _gear.Count > 0 ? _gear[0] : null;
            }
            _rows.Clear();
            foreach (ItemDrop.ItemData item in _gear)
            {
                ItemDrop.ItemData chosen = item;
                _rows.Add(new ListRow(item.GetIcon(), InscribePane.NameOf(item), item.m_quality.ToString(), Durability(item),
                    item == _item, dim: false, () => Choose(chosen), item));
            }
            view.Parts.List?.Fill(_rows);
            if (!Essences.IsOffered(_essence))
            {
                _essence = null;   // Biome Essences turned off while one of them was chosen
            }
            InscribePane.Fill(view, new InscribeChoice(_item, _rune, _essence));
        }

        /// <summary>Store all runes: every rune the player carries goes into the table.</summary>
        public override void Extra(TableView view) => StoreAll(view);

        /// <summary>A rune picked; with Shift the table's runes of that kind come out into the inventory (user 2026-10-07).</summary>
        public override void PickRune(int index)
        {
            _rune = StoneCatalog.BuiltInIds[index];
            if (TableWindow.ShiftHeld)
            {
                TakeOut(_rune);
            }
            TableWindow.MarkDirty();
        }

        // Slot 0 is the pure essence you have (nothing to choose); the essences follow, chosen only for the Ascension Rune.
        public override void PickEssence(int index)
        {
            if (index == 0 && TableWindow.ShiftHeld)
            {
                TakeEssence();
                return;
            }
            if (index < 1 || index > Essences.Offered || !TableIcons.Steers(TableIcons.Def(_rune)))
            {
                return;
            }
            Essence essence = Essences.All[index - 1];
            _essence = _essence == essence ? null : essence;
            TableWindow.MarkDirty();
        }

        public override void Act(TableView view, bool all)
        {
            ItemDrop.ItemData? rune = TableIcons.RuneItem(_rune);
            if (_item == null || rune == null)
            {
                return;
            }
            Essence? essence = TableIcons.Steers(TableIcons.Def(_rune)) && Essences.IsOffered(_essence) ? _essence : null;
            var supply = new TableSupply(view.Store, essence);
            StoneJob job = StoneJob.Create(view.Player, rune, _item, supply);
            StoneResult result = StonePipeline.Evaluate(job);
            if (result.Refused)
            {
                StoneFeedback.Show(view.Player, result.Refusal!);
                return;
            }
            if (result.NeedsInscription)
            {
                InscriptionChooser.Ask(job);
                return;
            }
            ConfirmGate.Pass(job, result, all);
        }

        private void Choose(ItemDrop.ItemData item)
        {
            _item = item;
            TableWindow.MarkDirty();
        }

        // Gear a rune could change, in the inventory's own order: top row first, left to right.
        private void Gather(Inventory inventory)
        {
            _gear.Clear();
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (ItemClasses.IsRuneBase(item))
                {
                    _gear.Add(item);
                }
            }
            _gear.Sort((a, b) => a.m_gridPos.y != b.m_gridPos.y ? a.m_gridPos.y.CompareTo(b.m_gridPos.y) : a.m_gridPos.x.CompareTo(b.m_gridPos.x));
        }

        private static float? Durability(ItemDrop.ItemData item) =>
            item.m_shared.m_useDurability && item.m_durability < item.GetMaxDurability() ? item.GetDurabilityPercentage() : (float?)null;
    }

    /// <summary>What the Inscribe tab has chosen: the gear, the rune, the essence (null for none).</summary>
    internal readonly struct InscribeChoice
    {
        public InscribeChoice(ItemDrop.ItemData? item, string rune, Essence? essence)
        {
            Item = item;
            Rune = rune;
            Essence = essence;
        }

        public ItemDrop.ItemData? Item { get; }
        public string Rune { get; }
        public Essence? Essence { get; }

        public StoneDef? Def => TableIcons.Def(Rune);

        /// <summary>The essence when the rune rolls inscriptions; null when none was chosen or the rune ignores it.</summary>
        public Essence? Steering => TableIcons.Steers(Def) ? Essence : null;

        public int EssenceCost => Item != null ? Essences.CostFor(ItemTier.Of(Item)) : 0;

        /// <summary>Runes one use costs on this item (the rune's cost for the item's rarity; 1 when unknown).</summary>
        public int RuneCost
        {
            get
            {
                StoneDef? def = Def;
                ItemState? state = Item != null ? ItemState.Read(Item) : null;
                string rarity = state != null && state.IsMagic ? state.RarityId ?? "" : ActiveRules.Current.Economy.BaseRarity?.Id ?? "";
                return def != null ? System.Math.Max(def.CostFor(rarity), 0) : 1;
            }
        }
    }
}
