using System.Collections.Generic;
using Mantle.Layout;
using Mantle.Slots;
using UnityEngine;

namespace Mantle.Panels
{
    /// <summary>
    /// Where each slot is drawn in the slot panel, as (column, row) of its cells under the tab buttons. Two tabs
    /// (<see cref="SlotTabs"/>) of the same size: Gear has Head, Chest, Legs, Back and the Backpack in a column on the
    /// left, the utilities, under them the Trinket slot and under that the coin purse in a column on the right (the user,
    /// 2026-10-12 21:32; without the Trinket slot the purse takes its place under the utilities, so no gap is left) and
    /// the stat sheet between them (<see cref="GearStats"/>);
    /// Consumables has a row each of food, mead and ammo from the top. A kind with no slots leaves its place empty in
    /// Gear and no row in Consumables, which is not offered at all without consumable slots. The key ring's button (the
    /// ring cells are drawn in its pop-up, <see cref="KeyRingPopup"/>) and the Tacklebox slot (its cells in its own pop-up,
    /// <see cref="Tackle.TacklePopup"/>) sit under the backpack in the left column. The ring cells and the belt cell (Mantle's, 2026-10-10) stand in a column of their
    /// own at the right edge of the Gear tab, beside the utilities and the trinket slot where PackPanel puts its worn extras;
    /// the panel is one cell wider with them, and without them it has no empty column; the stat sheet keeps its width. Only the shown tab's cells are in
    /// <see cref="Cells"/>; the others are hidden. Every group holds five at most, so the panel is five cells wide (six with the ring and belt column).
    /// </summary>
    public sealed class SlotPanelLayout
    {
        public const int MinColumns = 5;

        /// <summary>The Gear tab's rows at least: the left column's five kinds and room for the sheet; the right column may need one more.</summary>
        public const int GearRows = 5;

        private static readonly SlotKind[] LeftColumn = { SlotKind.Head, SlotKind.Chest, SlotKind.Legs, SlotKind.Feet, SlotKind.Back, SlotKind.Backpack };
        private static readonly SlotKind[] RightColumn = { SlotKind.Utility, SlotKind.Trinket, SlotKind.Purse };
        private static readonly SlotKind[] AccessoryColumn = { SlotKind.Ring, SlotKind.Belt };
        private static readonly SlotKind[] ConsumableRows = { SlotKind.Food, SlotKind.Mead, SlotKind.Ammo };

        public Dictionary<int, Vector2Int> Cells { get; } = new Dictionary<int, Vector2Int>();

        /// <summary>The tab drawn: the one asked for, or Gear while there are no consumable slots.</summary>
        public SlotTab Tab { get; private set; }

        public bool HasConsumables { get; private set; }

        public int Columns { get; private set; } = MinColumns;

        /// <summary>True while the ring and belt column is part of the panel (the layout has a ring or belt cell).</summary>
        public bool HasAccessories { get; private set; }

        /// <summary>The columns between the armour column and the right-hand groups, which the stat sheet fills.</summary>
        public int SheetColumns => Columns - (HasAccessories ? 3 : 2);

        /// <summary>The rows above the purse's, the same in both tabs so the panel keeps its size.</summary>
        public int ContentRows { get; private set; } = GearRows;

        public int Rows { get; private set; }

        /// <summary>The purse's row, the last, which also holds the key ring's button; -1 without either.</summary>
        public int PurseRow { get; private set; } = -1;

        /// <summary>Where the key ring's button is drawn; null without ring cells.</summary>
        public Vector2Int? RingCell { get; private set; }

        /// <summary>Where the Tacklebox slot is drawn, right of the ring's button; null without the slot. Its cells hang in its pop-up.</summary>
        public Vector2Int? TackleboxCell { get; private set; }

        public static SlotPanelLayout For(InventoryLayout layout, SlotTab tab)
        {
            SlotPanelLayout panel = new SlotPanelLayout();
            panel.HasAccessories = layout.CellsOf(SlotKind.Ring).Count > 0 || layout.CellsOf(SlotKind.Belt).Count > 0;
            // No empty sixth column without rings or belt (play test P2, 2026-10-13): the purse moved into the right column on 2026-10-12.
            panel.Columns = MinColumns + (panel.HasAccessories ? 1 : 0);
            int rows = panel.AddConsumables(layout, tab == SlotTab.Consumables);
            panel.HasConsumables = rows > 0;
            panel.Tab = panel.HasConsumables ? tab : SlotTab.Gear;
            int gear = panel.AddColumn(layout, RightColumn, panel.Columns - (panel.HasAccessories ? 2 : 1), panel.Tab == SlotTab.Gear);
            if (panel.HasAccessories)
                gear = Mathf.Max(gear, panel.AddColumn(layout, AccessoryColumn, panel.Columns - 1, panel.Tab == SlotTab.Gear));
            int left = panel.AddColumn(layout, LeftColumn, 0, panel.Tab == SlotTab.Gear);   // six rows with the Feet slot
            gear = Mathf.Max(gear, left);
            panel.ContentRows = panel.Tab == SlotTab.Gear ? Mathf.Max(GearRows, gear) : rows;
            panel.Rows = panel.ContentRows;
            if (panel.Tab == SlotTab.Gear) panel.AddPurseRow(layout, left);
            return panel;
        }

        /// <summary>A row each for food, mead and ammo that have slots, into <see cref="Cells"/> when shown; the rows used.</summary>
        private int AddConsumables(InventoryLayout layout, bool shown)
        {
            int rows = 0;
            foreach (SlotKind kind in ConsumableRows)
            {
                int column = 0;
                for (int i = 0; i < layout.Slots.Count; i++)
                {
                    if (layout.Slots[i].Kind != kind)
                        continue;
                    if (shown)
                        Cells[i] = new Vector2Int(column, rows);
                    column++;
                }
                Columns = Mathf.Max(Columns, column);
                rows += column > 0 ? 1 : 0;
            }
            return rows;
        }

        /// <summary>
        /// The slots of the kinds top down in one column, in the layout's order, into <see cref="Cells"/> when shown; the
        /// rows used, counted in both tabs so they keep the same size.
        /// </summary>
        private int AddColumn(InventoryLayout layout, SlotKind[] kinds, int column, bool shown)
        {
            int row = 0;
            for (int i = 0; i < layout.Slots.Count; i++)
            {
                if (System.Array.IndexOf(kinds, layout.Slots[i].Kind) < 0)
                    continue;
                if (shown)
                    Cells[i] = new Vector2Int(column, row);
                row++;
            }
            return row;
        }

        /// <summary>
        /// The key ring's button and the Tacklebox slot under the backpack in the left column, from row <paramref name="left"/>
        /// (the user, 2026-10-12 17:44). The purse is in the right column, under the Trinket.
        /// </summary>
        private void AddPurseRow(InventoryLayout layout, int left)
        {
            if (layout.CellsOf(SlotKind.Key).Count > 0) RingCell = Under();
            int box = layout.IndexOf(new Slot(SlotKind.Tacklebox, 1).Id);
            if (box >= 0) { TackleboxCell = Under(); Cells[box] = TackleboxCell.Value; }
            ContentRows = Rows;
            Vector2Int Under() => Grow(new Vector2Int(0, left++));
            Vector2Int Grow(Vector2Int cell) { Rows = Mathf.Max(Rows, cell.y + 1); return cell; }
        }
    }
}
