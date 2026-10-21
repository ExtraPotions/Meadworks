using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// What every move action shares: the stacks of the player inventory with a put under way (a request to a
    /// shared chest that has not answered yet; such a stack is skipped by every action and pinned by sorting so it
    /// cannot be moved twice), and the centre message at the end of an action. The moves themselves go through
    /// <see cref="Shared.ChestWriter"/>, batched per container by <see cref="ChestBatch"/>.
    /// </summary>
    public static class StackMover
    {
        private static readonly HashSet<ItemDrop.ItemData> pending = new HashSet<ItemDrop.ItemData>();

        /// <summary>The stack has a put under way; no action moves it until the answer comes.</summary>
        public static bool IsPending(ItemDrop.ItemData item) => item != null && pending.Contains(item);

        internal static void Reserve(ItemDrop.ItemData item)
        {
            if (item != null)
                pending.Add(item);
        }

        internal static void Release(ItemDrop.ItemData item)
        {
            if (item != null)
                pending.Remove(item);
        }

        /// <summary>The centre message of a move action: "Moved n stacks" or "Nothing to move".</summary>
        public static void Report(int stacks)
        {
            Messages.Center(stacks > 0 ? StowWords.Format(StowWords.Moved, stacks) : StowWords.Nothing);
        }

        /// <summary>
        /// The end of an action: what moved at once is reported now; when nothing did and shared chests still owe
        /// replies, nothing is said yet, the batches report when they are answered.
        /// </summary>
        public static void ReportAction(int stacks, bool waiting)
        {
            if (stacks > 0 || !waiting)
                Report(stacks);
        }
    }
}
