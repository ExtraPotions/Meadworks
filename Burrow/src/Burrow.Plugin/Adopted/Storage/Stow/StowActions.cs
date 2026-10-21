using System;
using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// Quick stack, store all and dump: the player's movable stacks go into containers, as far as they fit, every
    /// stack through the writer (<see cref="ChestBatch"/>). What moved at once is reported at the end; a shared
    /// chest reports itself when its replies are in. Equipped items, quest items, favourite items, favourite slots,
    /// the hotbar, items below the main grid (<see cref="MainGrid"/>) and stacks with a request under way stay; a
    /// container's <c>refuse</c> list is honoured.
    /// </summary>
    public static class StowActions
    {
        /// <summary>The module is on and there is a local player who is not teleporting; says so otherwise.</summary>
        public static bool Ready(out Player player)
        {
            player = Player.m_localPlayer;
            if (!StowSettings.Enabled.Value)
            {
                Messages.Center(StowWords.Off);
                return false;
            }
            return player != null && !player.IsTeleporting();
        }

        /// <summary>Stacks whose item the open container already holds go there; with Quick Stack Nearby, or with no
        /// container open, every nearby container that holds the item is a target too.</summary>
        public static void QuickStack()
        {
            if (!Ready(out Player player))
                return;
            bool nearby = StowSettings.QuickStackNearby.Value;
            List<Container> targets = nearby ? StowTargets.Nearby(player) : StowTargets.OpenOnly();
            if (targets.Count == 0)
            {
                if (nearby)
                    Messages.Center(StowWords.Nothing);
                else
                    StowTargets.SayNoOpen();
                return;
            }
            StackInto(player, targets, true);
        }

        /// <summary>Quick stack only into the interacted chest, without opening it or reaching nearby targets.</summary>
        public static bool QuickStackTo(Container container)
        {
            if (!Ready(out Player player) || !StowTargets.IsTarget(container))
                return false;
            StackInto(player, new List<Container> { container }, true);
            return true;
        }

        /// <summary>Quick stack to every nearby container from outside the inventory.</summary>
        public static void Dump()
        {
            if (!Ready(out Player player))
                return;
            if (!StowSettings.QuickStackNearby.Value)
            {
                Messages.Center(StowWords.NearbyOff);
                return;
            }
            StackInto(player, StowTargets.Nearby(player), true);
        }

        /// <summary>Every movable item goes into the open container as far as it fits.</summary>
        public static void StoreAll()
        {
            if (!Ready(out Player player))
                return;
            List<Container> targets = StowTargets.OpenOnly();
            if (targets.Count == 0)
            {
                StowTargets.SayNoOpen();
                return;
            }
            StackInto(player, targets, false);
        }

        /// <summary>The game's Take all on the open container, through the button's own action, so a shared chest
        /// gets its request and View mode refuses exactly as the button does.</summary>
        public static void TakeAll(InventoryGui gui)
        {
            if (!Ready(out Player _))
                return;
            if (StowTargets.Open == null)
            {
                Messages.Center(StowWords.NoContainer);
                return;
            }
            gui.OnTakeAll();
        }

        /// <summary>
        /// Moves the movable stacks into the targets in order; with <paramref name="onlyHeld"/> only into containers
        /// that already hold the item. Reports the stacks that moved at least partly. The targets come nearest first,
        /// so what a full container leaves of a stack is still in the inventory when the next container holding the
        /// item comes, and goes there. A stack sent to a shared chest waits for its answer (later containers skip it
        /// meanwhile), then what the chest did not take goes on to the later containers holding it (<see cref="SpillOver"/>).
        /// </summary>
        private static void StackInto(Player player, List<Container> targets, bool onlyHeld)
        {
            int stacks = 0;
            bool waiting = false;
            for (int i = 0; i < targets.Count; i++)
            {
                ChestBatch batch = new ChestBatch(targets[i], StowWords.MovedTo);
                List<Container> later = batch.Shared ? targets.GetRange(i + 1, targets.Count - i - 1) : null;
                foreach (ItemDrop.ItemData item in Candidates(player, targets[i], onlyHeld))
                    batch.Put(item, item.m_stack, 1, later != null && later.Count > 0 ? () => SpillOver(player, item, later) : (Action)null);
                batch.Finish();
                if (batch.Waiting)
                    waiting = true;
                else
                    stacks += batch.Moved;
            }
            StackMover.ReportAction(stacks, waiting);
        }

        /// <summary>
        /// A stack a shared chest has answered for: what it did not take goes on to the later containers of the action
        /// that hold the item, nearest first. The chest's own summary stays in the centre; this says in the top left
        /// where the rest went, only when something moved.
        /// </summary>
        private static void SpillOver(Player player, ItemDrop.ItemData item, List<Container> later)
        {
            string name = ItemNames.DisplayName(item);
            Overflow.Send(player, item, item.m_stack, null, later, sent =>
            {
                if (sent.Took.Count > 0)
                    Messages.TopLeft(Routing.SentWords(sent, name));
            });
        }

        private static List<ItemDrop.ItemData> Candidates(Player player, Container container, bool onlyHeld)
        {
            Inventory inventory = player.GetInventory();
            Inventory target = container.GetInventory();
            int first = MainGrid.FirstRow(player, inventory);
            int rows = MainGrid.Rows(player, inventory);
            List<ItemDrop.ItemData> list = new List<ItemDrop.ItemData>();
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(inventory.GetAllItems()))
            {
                if (item.m_gridPos.y < first || item.m_gridPos.y >= rows)
                    continue;
                if (!Movable.CanMove(player, inventory, item) || StowRules.Refuses(container, item))
                    continue;
                if (onlyHeld && !target.ContainsItemByName(item.m_shared.m_name))
                    continue;
                list.Add(item);
            }
            return list;
        }
    }
}
