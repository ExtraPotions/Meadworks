using System;
using System.Collections.Generic;
using Burrow.Core;
using Burrow.Shared;

namespace Burrow.Stow
{
    /// <summary>
    /// Sorts an inventory by <c>Sort Order</c>: stacks of the same item merge, pinned items keep their cell, the
    /// rest are written into the free cells in order (row by row) and the inventory reports the change. In the
    /// player inventory the hotbar row, the rows below the main grid (<see cref="MainGrid"/>: nothing is taken from
    /// them or put there), favourite slots, favourite items, equipped items and stacks with a request under way are pinned (the rest sort
    /// around them); a container pins nothing. A chest another player is using cannot be sorted: a sort rewrites every
    /// cell at once, which no request carries, so it is refused with a message (auto sort skips it quietly).
    /// </summary>
    public static class Sorting
    {
        public static void SortPlayer(Player player, bool quiet)
        {
            if (player == null || !StowSettings.Enabled.Value)
                return;
            Inventory inventory = player.GetInventory();
            int stacks = Sort(inventory, item => Pinned(player, item), MainGrid.FirstRow(player, inventory), MainGrid.Rows(player, inventory),
                PackPanelGrid.BlockedCells(player, inventory));
            if (!quiet)
                Messages.Center(StowWords.Format(StowWords.Sorted, stacks));
        }

        public static void SortPlayerAndOpenContainer()
        {
            if (!StowActions.Ready(out Player player))
                return;
            InventoryGui gui = InventoryGui.instance;
            Container open = gui != null && gui.IsContainerOpen() ? StowTargets.Open : null;
            SortPlayer(player, false);
            if (open != null)
                SortContainer(open, false);
        }

        public static void SortOpenContainer()
        {
            if (!StowActions.Ready(out Player _))
                return;
            SortContainer(StowTargets.Open, false);
        }

        public static void SortContainer(Container container, bool quiet)
        {
            if (container == null || !ContainerScan.IsUsable(container, ContainerUse.Stow))
            {
                if (!quiet)
                    Messages.Center(WhyNot(container));
                return;
            }
            if (!ContainerScan.Claim(container))
                return;
            Inventory inventory = container.GetInventory();
            int stacks = Sort(inventory, item => false, 0, inventory.GetHeight());
            ContainerScan.Save(container);
            if (!quiet)
                Messages.Center(StowWords.Format(StowWords.Sorted, stacks));
        }

        /// <summary>"Viewing only" for a chest the player views, "The chest cannot be changed right now" for one
        /// another player uses in Full mode, else "No container is open".</summary>
        private static string WhyNot(Container container)
        {
            if (container == null)
                return StowWords.NoContainer;
            if (StowTargets.IsShared(container))
                return SharedWords.Unavailable;
            return SharedState.IsViewing(container) ? SharedWords.ReadOnly : StowWords.NoContainer;
        }

        private static bool Pinned(Player player, ItemDrop.ItemData item)
        {
            if (PackPanelGrid.InSlot(player, player.GetInventory(), item)) return true;
            // Changed by expDARE, 2026-10-20: a favourite item always keeps its cell (the Sort Favorite Items setting is gone).
            return item.m_equipped || player.IsItemEquiped(item) || Favourites.IsFavouriteSlot(item.m_gridPos) || Favourites.IsFavouriteItem(item)
                || StackMover.IsPending(item);
        }

        /// <summary>Sorts the loose items into the cells of the rows from <paramref name="firstRow"/> up to
        /// <paramref name="rows"/>, never into a <paramref name="reserved"/> cell (a backpack's blocked cells); items
        /// outside those rows stay. Returns the number of loose items.</summary>
        public static int Sort(Inventory inventory, Func<ItemDrop.ItemData, bool> pinned, int firstRow, int rows, IEnumerable<Vector2i> reserved = null)
        {
            List<ItemDrop.ItemData> all = inventory.GetAllItems();
            List<ItemDrop.ItemData> loose = new List<ItemDrop.ItemData>();
            int width = inventory.GetWidth();
            HashSet<int> taken = new HashSet<int>();
            foreach (Vector2i cell in reserved ?? new Vector2i[0])
                taken.Add(cell.y * width + cell.x);
            foreach (ItemDrop.ItemData item in all)
            {
                if (pinned(item) || item.m_gridPos.y < firstRow || item.m_gridPos.y >= rows)
                    taken.Add(item.m_gridPos.y * width + item.m_gridPos.x);
                else
                    loose.Add(item);
            }
            Merge(loose, all);
            Dictionary<string, int> totals = Totals(loose);
            loose.Sort((a, b) => Compare(a, b, totals));
            Place(inventory, loose, taken, firstRow * width, rows * width);
            inventory.Changed();
            return loose.Count;
        }

        /// <summary>Pours later stacks of the same item into earlier ones; emptied stacks leave the inventory.</summary>
        private static void Merge(List<ItemDrop.ItemData> loose, List<ItemDrop.ItemData> all)
        {
            for (int i = 0; i < loose.Count; i++)
            {
                ItemDrop.ItemData into = loose[i];
                if (into.m_shared.m_maxStackSize <= 1)
                    continue;
                for (int j = loose.Count - 1; j > i; j--)
                {
                    int space = into.m_shared.m_maxStackSize - into.m_stack;
                    if (space <= 0)
                        break;
                    ItemDrop.ItemData from = loose[j];
                    if (!Same(into, from))
                        continue;
                    int move = Math.Min(space, from.m_stack);
                    into.m_stack += move;
                    from.m_stack -= move;
                    if (from.m_stack == 0 && all.Remove(from))
                        loose.RemoveAt(j);
                }
            }
        }

        private static void Place(Inventory inventory, List<ItemDrop.ItemData> loose, HashSet<int> taken, int firstCell, int cells)
        {
            int width = inventory.GetWidth();
            int cell = firstCell;
            foreach (ItemDrop.ItemData item in loose)
            {
                while (cell < cells && taken.Contains(cell))
                    cell++;
                if (cell >= cells)
                    break;
                item.m_gridPos = new Vector2i(cell % width, cell / width);
                cell++;
            }
        }

        private static bool Same(ItemDrop.ItemData a, ItemDrop.ItemData b)
        {
            return ItemNames.SameItem(a, b) && a.m_quality == b.m_quality && a.m_worldLevel == b.m_worldLevel
                && a.m_variant == b.m_variant && a.m_crafterID == b.m_crafterID && a.m_crafterName == b.m_crafterName
                && SameMetadata(a.m_customData, b.m_customData);
        }

        private static bool SameMetadata(Dictionary<string, string> left, Dictionary<string, string> right)
        {
            if ((left?.Count ?? 0) != (right?.Count ?? 0)) return false;
            if (left == null || left.Count == 0) return true;
            foreach (KeyValuePair<string, string> entry in left)
                if (!right.TryGetValue(entry.Key, out string value) || !string.Equals(entry.Value, value, StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>How many of each item the loose stacks hold together, by shared name, for the Amount order.</summary>
        private static Dictionary<string, int> Totals(List<ItemDrop.ItemData> loose)
        {
            Dictionary<string, int> totals = new Dictionary<string, int>();
            foreach (ItemDrop.ItemData item in loose)
            {
                totals.TryGetValue(item.m_shared.m_name, out int count);
                totals[item.m_shared.m_name] = count + item.m_stack;
            }
            return totals;
        }

        private static int Compare(ItemDrop.ItemData a, ItemDrop.ItemData b, Dictionary<string, int> totals)
        {
            int result = 0;
            switch (StowSettings.SortOrder.Value)
            {
                case SortOrder.Category: result = ((int)a.m_shared.m_itemType).CompareTo((int)b.m_shared.m_itemType); break;
                case SortOrder.Weight: result = b.m_shared.m_weight.CompareTo(a.m_shared.m_weight); break;
                case SortOrder.Value: result = b.m_shared.m_value.CompareTo(a.m_shared.m_value); break;
                case SortOrder.Amount: result = totals[b.m_shared.m_name].CompareTo(totals[a.m_shared.m_name]); break;
            }
            if (result == 0)
                result = string.Compare(ItemNames.DisplayName(a), ItemNames.DisplayName(b), StringComparison.OrdinalIgnoreCase);
            if (result == 0)
                result = b.m_quality.CompareTo(a.m_quality);
            if (result == 0)
                result = b.m_stack.CompareTo(a.m_stack);
            return result;
        }
    }
}
