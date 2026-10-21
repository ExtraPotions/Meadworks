using System;
using System.Collections.Generic;
using Burrow.Shared;

namespace Burrow.Stow
{
    /// <summary>
    /// One stack sent on until it is placed: it goes into the chosen container, and what does not fit goes on to the
    /// next container of the line that holds the item, and so on until the amount is placed or the line ends. The line
    /// is the caller's list of nearby targets (the open container first, then nearest first from the player), so the rest
    /// always lands in the closest container holding the item. Every step is one put through <see cref="ChestWriter"/>
    /// with the stack reserved in <see cref="StackMover"/> until its answer, so a shared chest (Full mode, another
    /// player using it) is a step like any other and the line goes on when its answer arrives. Each container is checked
    /// again when its turn comes (still a target, not refusing the item, still holding it), because a shared chest's
    /// answer takes time, and so is the stack (still in the inventory, still free to move).
    /// </summary>
    internal sealed class Overflow
    {
        private readonly Player player;
        private readonly ItemDrop.ItemData item;
        private readonly List<Container> line = new List<Container>();
        private readonly int chosen;
        private readonly int fallback = -1;
        private readonly Action<Overflow> done;
        private int left;
        private int next;

        private Overflow(Player player, ItemDrop.ItemData item, int amount, Container first, List<Container> rest, Container last,
            Action<Overflow> done)
        {
            this.player = player;
            this.item = item;
            this.done = done;
            left = amount;
            if (first != null)
                line.Add(first);
            chosen = line.Count;
            foreach (Container container in rest)
            {
                if (container != first && container != last)
                    line.Add(container);
            }
            if (last != null && last != first)
            {
                fallback = line.Count;
                line.Add(last);
            }
        }

        /// <summary>The containers that took part of the stack, in the order they took it.</summary>
        public List<Container> Took { get; } = new List<Container>();

        /// <summary>The last container asked was a shared chest that said no; the writer has already said why.</summary>
        public bool RefusalSaid { get; private set; }

        /// <summary>
        /// Sends up to <paramref name="amount"/> units of a player inventory stack: into <paramref name="first"/> (when
        /// given) whatever it holds, then into each container of <paramref name="rest"/> that holds the item, in order,
        /// and last into <paramref name="last"/> (when given) whatever it holds.
        /// <paramref name="done"/> runs once, when the amount is placed or the line has ended, at once or after the last answer.
        /// </summary>
        public static void Send(Player player, ItemDrop.ItemData item, int amount, Container first, List<Container> rest, Action<Overflow> done,
            Container last = null)
        {
            new Overflow(player, item, amount, first, rest, last, done).Step();
        }

        private void Step()
        {
            Container target = NextTarget();
            if (target == null)
            {
                done?.Invoke(this);
                return;
            }
            int before = Units();
            bool shared = StowTargets.IsShared(target);
            StackMover.Reserve(item);
            ChestWriter.Put(target, item, Math.Min(left, before), null, ok => Answered(target, before, shared, ok));
        }

        private void Answered(Container target, int before, bool shared, bool ok)
        {
            StackMover.Release(item);
            int moved = Math.Max(0, before - Units());
            if (moved > 0)
            {
                left -= moved;
                Took.Add(target);
            }
            RefusalSaid = shared && !ok;
            Step();
        }

        private Container NextTarget()
        {
            if (player == null || player != Player.m_localPlayer)
                return null;
            while (next < line.Count && left > 0 && Units() > 0 && Movable.CanMove(player, player.GetInventory(), item))
            {
                Container candidate = line[next];
                bool mustHold = next >= chosen && next != fallback;
                next++;
                if (Takes(candidate, mustHold))
                    return candidate;
            }
            return null;
        }

        private bool Takes(Container container, bool mustHold)
        {
            if (container == null || !StowTargets.IsTarget(container) || StowRules.Refuses(container, item)
                || !container.GetInventory().CanAddItem(item, 1))
                return false;
            return !mustHold || container.GetInventory().ContainsItemByName(item.m_shared.m_name);
        }

        /// <summary>The units of the stack still in the player's inventory (the game removes a stack that moved whole).</summary>
        private int Units()
        {
            Inventory inventory = player != null ? player.GetInventory() : null;
            return inventory != null && inventory.ContainsItem(item) ? item.m_stack : 0;
        }
    }
}
