using System;

namespace Burrow.Core
{
    /// <summary>
    /// When a table of per-object entries (chests, fires) is pruned of its dead entries: once it has grown past a mark,
    /// which then moves to twice what is left, never below the floor. A table that keeps many live entries is so walked
    /// once per doubling, not on every new entry, and pruning costs the same small amount per entry added however big
    /// the base is.
    /// </summary>
    public sealed class PruneMark
    {
        private readonly int floor;
        private int mark;

        public PruneMark(int floor)
        {
            this.floor = floor;
            mark = floor;
        }

        /// <summary>The table holds more than the mark: prune it now, then report what is left with <see cref="Pruned"/>.</summary>
        public bool Due(int count) => count > mark;

        public void Pruned(int left) => mark = Math.Max(floor, left * 2);
    }
}
