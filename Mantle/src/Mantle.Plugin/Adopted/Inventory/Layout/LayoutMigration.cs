using System.Collections.Generic;
using Mantle.Accessories;
using Mantle.Ring;
using Mantle.Tackle;
using Mantle.Slots;

namespace Mantle.Layout
{
    /// <summary>
    /// Where every item goes when the layout changes, worked out before anything moves. An item in a slot keeps its
    /// slot (by id) when the new layout still has it and the slot still takes the item; an item in the main grid keeps
    /// its cell when that cell is still in the main grid. A key kept in the main grid moves into its ring cell when this
    /// layout adds that cell (the ring switched on, the key added to Key Items), so switching the ring off and on brings
    /// the keys back; a key a player left in the grid under the same ring stays there. Bait kept in the main grid moves
    /// likewise into tacklebox cells this layout adds (<see cref="GatherTackle"/>). Everything else is displaced: a key
    /// goes to its free ring cell, bait to a free tacklebox cell, the rest to the free main cells,
    /// bottom row first as the game places what it picks up, so the hotbar stays as it was. What still does not fit
    /// is overflow: the caller drops it, or parks it below the layout while the character is still loading.
    /// Two items claiming one cell (a broken save) leave the second displaced, so no item is ever hidden under another.
    /// </summary>
    public sealed class LayoutMigration
    {
        private readonly InventoryLayout to;
        private readonly HashSet<Vector2i> taken = new HashSet<Vector2i>();
        private readonly Dictionary<ItemDrop.ItemData, Vector2i> targets = new Dictionary<ItemDrop.ItemData, Vector2i>();

        private LayoutMigration(InventoryLayout to)
        {
            this.to = to;
        }

        public List<ItemDrop.ItemData> Overflow { get; } = new List<ItemDrop.ItemData>();

        public int Moved { get; private set; }

        public static LayoutMigration Plan(IEnumerable<ItemDrop.ItemData> items, InventoryLayout from, InventoryLayout to)
        {
            LayoutMigration plan = new LayoutMigration(to);
            List<ItemDrop.ItemData> displaced = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in items)
            {
                if (!plan.TryKeep(item, from))
                    displaced.Add(item);
            }
            plan.GatherKeys(from);
            plan.GatherTackle(from);
            foreach (ItemDrop.ItemData item in displaced)
            {
                if (!plan.TryAccessory(item) && !plan.TryRing(item) && !plan.TryTackle(item) && !plan.TryMain(item))
                    plan.Overflow.Add(item);
            }
            return plan;
        }

        /// <summary>Moves every placed item to its cell and parks the overflow in the rows below the layout.</summary>
        public void Apply()
        {
            foreach (KeyValuePair<ItemDrop.ItemData, Vector2i> target in targets)
            {
                if (target.Key.m_gridPos != target.Value)
                    Moved++;
                target.Key.m_gridPos = target.Value;
            }
            for (int i = 0; i < Overflow.Count; i++)
                Overflow[i].m_gridPos = new Vector2i(i % to.Width, to.Height + i / to.Width);
        }

        private bool TryKeep(ItemDrop.ItemData item, InventoryLayout from)
        {
            Vector2i pos = item.m_gridPos;
            Slot slot = from.SlotAt(pos);
            if (slot != null)
            {
                if (BelongsToOtherCell(item, slot.Kind))
                    return false;
                int index = to.IndexOf(slot.Id);
                return index >= 0 && SlotRules.Accepts(slot, item) && TryTake(item, to.CellOf(index));
            }
            return from.IsMain(pos) && to.IsMain(pos) && TryTake(item, pos);
        }

        /// <summary>
        /// A tagged ring or belt (the Megingjord too) lying in a slot of another kind, while the new layout has a cell of its
        /// own kind: it is displaced, and <see cref="TryAccessory"/> gives it that cell. With no such cell it keeps its old slot.
        /// </summary>
        private bool BelongsToOtherCell(ItemDrop.ItemData item, SlotKind current)
        {
            SlotKind? kind = AccessoryItems.KindOf(item);
            return kind.HasValue && kind.Value != current && to.CellsOf(kind.Value).Count > 0;
        }

        /// <summary>A displaced ring or belt goes to a free cell of its own kind before the grid.</summary>
        private bool TryAccessory(ItemDrop.ItemData item)
        {
            SlotKind? kind = AccessoryItems.KindOf(item);
            if (!kind.HasValue)
                return false;
            foreach (Vector2i cell in to.CellsOf(kind.Value))
            {
                if (TryTake(item, cell))
                    return true;
            }
            return false;
        }

        /// <summary>Keys kept in a main cell move into their ring cell when the old layout had no such cell.</summary>
        private void GatherKeys(InventoryLayout from)
        {
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(targets.Keys))
            {
                Vector2i ring = KeyRing.CellOf(to, item);
                Vector2i kept = targets[item];
                if (ring.x < 0 || !to.IsMain(kept) || from.IndexOf(new Slot(SlotKind.Key, KeyRing.NumberOf(item)).Id) >= 0 || !taken.Add(ring))
                    continue;
                taken.Remove(kept);
                targets[item] = ring;
            }
        }

        private bool TryRing(ItemDrop.ItemData item)
        {
            Vector2i ring = KeyRing.CellOf(to, item);
            return ring.x >= 0 && TryTake(item, ring);
        }

        /// <summary>
        /// Bait kept in a main cell moves into a tacklebox cell the old layout did not have (a box put in its slot, or a
        /// bigger box), first come first served, so a new box fills with the bait already carried.
        /// </summary>
        private void GatherTackle(InventoryLayout from)
        {
            Queue<Vector2i> added = AddedTackleCells(from);
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(targets.Keys))
            {
                if (added.Count == 0)
                    return;
                Vector2i kept = targets[item];
                if (!to.IsMain(kept) || !TackleRules.IsTackle(item))
                    continue;
                Vector2i cell = added.Dequeue();
                taken.Remove(kept);
                taken.Add(cell);
                targets[item] = cell;
            }
        }

        /// <summary>The new layout's tackle cells whose ids the old one lacks and nothing kept has taken, in order.</summary>
        private Queue<Vector2i> AddedTackleCells(InventoryLayout from)
        {
            Queue<Vector2i> added = new Queue<Vector2i>();
            IReadOnlyList<Vector2i> cells = to.CellsOf(SlotKind.Tackle);
            for (int number = 1; number <= cells.Count; number++)
            {
                if (from.IndexOf(new Slot(SlotKind.Tackle, number).Id) < 0 && !taken.Contains(cells[number - 1]))
                    added.Enqueue(cells[number - 1]);
            }
            return added;
        }

        /// <summary>Displaced bait goes to a free cell of the tacklebox before the grid.</summary>
        private bool TryTackle(ItemDrop.ItemData item)
        {
            if (!TackleRules.IsTackle(item))
                return false;
            foreach (Vector2i cell in to.CellsOf(SlotKind.Tackle))
            {
                if (TryTake(item, cell))
                    return true;
            }
            return false;
        }

        private bool TryMain(ItemDrop.ItemData item)
        {
            for (int row = 0; row < to.MainRows; row++)
            {
                int y = (row + 1) % to.MainRows;
                for (int column = 0; column < to.Width; column++)
                {
                    int x = y == 0 && to.Width > 8 ? (column + 8) % to.Width : column;
                    if (to.IsMain(new Vector2i(x, y)) && TryTake(item, new Vector2i(x, y)))
                        return true;
                }
            }
            return false;
        }

        private bool TryTake(ItemDrop.ItemData item, Vector2i cell)
        {
            if (!taken.Add(cell))
                return false;
            targets[item] = cell;
            return true;
        }
    }
}
