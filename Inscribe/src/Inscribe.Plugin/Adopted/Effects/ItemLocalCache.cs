// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;

namespace ExtraPotions.Inscribe.Adopted.Effects
{
    /// <summary>
    /// The item's <see cref="ItemLocalSums"/>, kept on the <see cref="ItemState"/> they were built from
    /// (<see cref="ItemState.LocalSums"/>). The state cache hands out a new state instance whenever the item's data is
    /// written, reloaded by the game or re-resolved against new rules, so the numbers can never be stale and need no
    /// table of their own.
    /// <para>
    /// Hot path: a plain item (no custom data) costs one dictionary-count check inside <see cref="ItemState.Read"/>;
    /// an item with state costs that one cache lookup and a field read. No allocation after the first sight of a state.
    /// </para>
    /// </summary>
    internal static class ItemLocalCache
    {
        /// <summary>Marks a state that has nothing item-local, so it is not summed again.</summary>
        private static readonly object None = new object();

        /// <summary>The item's local numbers, or null when it has none or affix effects are off (the getters then do nothing).</summary>
        public static ItemLocalSums? Get(ItemDrop.ItemData? item)
        {
            if (item == null || !ItemEffects.Enabled)
            {
                return null;
            }
            ItemState state = ItemState.Read(item);
            if (state.IsEmpty)
            {
                return null;
            }
            object? sums = state.LocalSums;
            if (sums == null)
            {
                sums = (object?)ItemLocalSums.Build(item, state) ?? None;
                state.LocalSums = sums;
            }
            return sums as ItemLocalSums;
        }
    }
}
