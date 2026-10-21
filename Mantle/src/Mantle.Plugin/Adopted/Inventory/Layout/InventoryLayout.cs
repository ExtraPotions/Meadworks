using System.Collections.Generic;
using System.Globalization;
using System;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// Where everything sits in the player's own <c>Inventory</c>: the main grid is the first <see cref="MainRows"/>
    /// rows, <see cref="Width"/> wide; the slots follow in the rows below it, in <see cref="Slots"/> order, filling each
    /// row left to right. Only the cells matter here; where a slot is drawn is <see cref="SlotPanelLayout"/>'s business.
    /// Keeping the slots in the game's own inventory means the game saves them, weighs them and puts them in the
    /// tombstone like any item. The record ("1|8|6|head1,chest1,...") is kept in the character's data so the next
    /// layout knows where each slot's item was.
    /// </summary>
    public sealed class InventoryLayout
    {
        private const string Version = "1";

        private Dictionary<SlotKind, List<Vector2i>> cellsByKind;

        /// <param name="baseCells">The main cells that stay without the backpack, counted from the top left; left out,
        /// every row above the backpack's.</param>
        public InventoryLayout(int width, int mainRows, IReadOnlyList<Slot> slots, int backpackSlots = 0, int baseCells = -1)
        {
            Width = Math.Max(1, Math.Min(12, width));
            MainRows = Math.Max(0, Math.Min(128, mainRows));
            Slots = slots ?? new List<Slot>();
            BackpackSlots = Math.Max(0, Math.Min(backpackSlots, MainRows * Width));
            int fullRows = MainRows - (BackpackSlots + Width - 1) / Width;
            BaseCells = baseCells >= 0 ? Math.Min(baseCells, MainRows * Width - BackpackSlots) : fullRows * Width;
        }

        public int Width { get; }

        /// <summary>The main grid's rows, the worn backpack's included.</summary>
        public int MainRows { get; }

        /// <summary>
        /// The main cells the worn backpack adds. Not in the record: a parsed layout says 0, and only the layout in use
        /// (<see cref="InventoryState.Layout"/>) is asked.
        /// </summary>
        public int BackpackSlots { get; }

        /// <summary>
        /// The main cells that stay without the backpack, from the top left: whole rows, or with Inventory Rows at 0 the
        /// two hands. The backpack's cells follow them in reading order, so they fill the rest of a hands row first.
        /// </summary>
        public int BaseCells { get; }

        /// <summary>The rows the base cells take, the last one perhaps only partly (the hands).</summary>
        public int BaseRows => (BaseCells + Width - 1) / Width;

        /// <summary>The main cells no item may use, the right end of the bottom row: what the hands or a backpack leave of it.</summary>
        public int BlockedCells => MainRows * Width - BaseCells - BackpackSlots;

        /// <summary>A main cell the worn backpack adds (it leaves with the backpack).</summary>
        public bool IsPackCell(Vector2i pos) => IsMain(pos) && pos.y * Width + pos.x >= BaseCells;

        public IReadOnlyList<Slot> Slots { get; }

        public int SlotRows => (Slots.Count + Width - 1) / Width;

        public int Height => MainRows + SlotRows;

        public Vector2i CellOf(int index) => new Vector2i(index % Width, MainRows + index / Width);

        public bool IsMain(Vector2i pos) => pos.x >= 0 && pos.x < Width && pos.y >= 0 && pos.y < MainRows && !IsBlocked(pos);

        public bool IsBlocked(Vector2i pos) => BlockedCells > 0 && pos.y == MainRows - 1 && pos.x >= Width - BlockedCells && pos.x < Width;

        /// <summary>The slot at a cell, or null for a main cell or a cell outside every slot.</summary>
        public Slot SlotAt(Vector2i pos)
        {
            if (pos.x < 0 || pos.x >= Width || pos.y < MainRows || pos.y >= Height)
                return null;
            int index = (pos.y - MainRows) * Width + pos.x;
            return index < Slots.Count ? Slots[index] : null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Id == id)
                    return i;
            }
            return -1;
        }

        /// <summary>The cells of every slot of a kind, in number order (worked out once per layout).</summary>
        public IReadOnlyList<Vector2i> CellsOf(SlotKind kind)
        {
            if (cellsByKind == null)
            {
                cellsByKind = new Dictionary<SlotKind, List<Vector2i>>();
                foreach (SlotKind each in Enum.GetValues(typeof(SlotKind)))
                    cellsByKind[each] = new List<Vector2i>();
                for (int i = 0; i < Slots.Count; i++)
                    cellsByKind[Slots[i].Kind].Add(CellOf(i));
            }
            return cellsByKind[kind];
        }

        public string Record()
        {
            List<string> ids = new List<string>(Slots.Count);
            foreach (Slot slot in Slots)
                ids.Add(slot.Id);
            return string.Join("|", Version, Width.ToString(CultureInfo.InvariantCulture),
                MainRows.ToString(CultureInfo.InvariantCulture), string.Join(",", ids));
        }

        /// <summary>A record written by <see cref="Record"/>, or null when there is none or it cannot be read.</summary>
        public static InventoryLayout Parse(string record)
        {
            if (record == null || record.Length > 4096) return null;
            string[] parts = record.Split('|');
            if (parts.Length != 4 || parts[0] != Version)
                return null;
            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width)
                || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rows))
                return null;
            if (width < 1 || width > 12 || rows < 0 || rows > 128) return null;
            List<Slot> slots = new List<Slot>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in parts[3].Split(','))
            {
                if (id.Length > 0)
                    {
                    if (id.Length > 128 || slots.Count >= 128 || !ids.Add(id)) return null;
                    var parsed = ParseId(id);
                    if (parsed != null && parsed.Number < 1) return null;
                    slots.Add(parsed ?? new Slot(id));
                }
            }
            return new InventoryLayout(width, rows, slots);
        }

        public bool SameAs(InventoryLayout other) => other != null && other.Record() == Record();

        private static Slot ParseId(string id)
        {
            foreach (SlotKind kind in Enum.GetValues(typeof(SlotKind)))
            {
                string name = kind.ToString().ToLowerInvariant();
                if (kind == SlotKind.Retired)
                    continue;
                if (id.StartsWith(name, StringComparison.Ordinal) && int.TryParse(id.Substring(name.Length), out int number))
                    return new Slot(kind, number);
            }
            return null;
        }
    }
}
