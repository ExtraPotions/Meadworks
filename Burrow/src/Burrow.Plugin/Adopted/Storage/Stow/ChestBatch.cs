using System;
using Burrow.Core;
using Burrow.Shared;

namespace Burrow.Stow
{
    /// <summary>
    /// One action's writes to one container, every one through <see cref="ChestWriter"/>. A container the local
    /// client can change now answers each call at once, so after <see cref="Finish"/> the caller reads
    /// <see cref="Moved"/> and reports itself. A shared chest (Full mode, another player using it) answers by
    /// request: the batch is then <see cref="Waiting"/>, the credits are summed as the replies arrive and one
    /// message, the batch's word with the count and the chest name, is shown after the last reply when anything
    /// moved; a refusal is already said by the writer, so nothing is said twice. Every put registers its stack in
    /// <see cref="StackMover"/> until its answer, so no later target and no later action moves it as well.
    /// </summary>
    internal sealed class ChestBatch
    {
        private readonly Container container;
        private readonly string word;
        private int pending;
        private bool finished;

        public ChestBatch(Container container, string word)
        {
            this.container = container;
            this.word = word;
            Shared = StowTargets.IsShared(container);
        }

        /// <summary>The writes go through requests; the answers come later.</summary>
        public bool Shared { get; }

        /// <summary>The credits of the calls that succeeded so far.</summary>
        public int Moved { get; private set; }

        /// <summary>After Finish: replies are still to come, the batch reports itself.</summary>
        public bool Waiting => pending > 0;

        /// <summary>
        /// Puts <paramref name="amount"/> of a player inventory stack into the container; <paramref name="credit"/> counts
        /// on success. <paramref name="after"/>, when given, runs once the answer is in and the stack is free again.
        /// </summary>
        public void Put(ItemDrop.ItemData item, int amount, int credit, Action after = null)
        {
            pending++;
            StackMover.Reserve(item);
            ChestWriter.Put(container, item, amount, null, ok =>
            {
                StackMover.Release(item);
                Reply(ok, credit);
                after?.Invoke();
            });
        }

        /// <summary>Takes <paramref name="amount"/> of a container stack into the player's inventory; <paramref name="credit"/> counts on success.</summary>
        public void Take(ItemDrop.ItemData item, int amount, int credit, Vector2i? slot = null)
        {
            pending++;
            ChestWriter.TakeTo(container, item, amount, slot ?? ChestOps.NoSlot, ok => Reply(ok, credit));
        }

        /// <summary>No more calls follow; a batch still waiting reports itself when its last reply arrives.</summary>
        public void Finish()
        {
            finished = true;
        }

        private void Reply(bool ok, int credit)
        {
            pending--;
            if (ok)
                Moved += credit;
            if (finished && pending == 0)
                Report();
        }

        private void Report()
        {
            if (Moved > 0)
                Messages.Center(StowWords.Format(word, Moved, StowTargets.Name(container)));
        }
    }
}
