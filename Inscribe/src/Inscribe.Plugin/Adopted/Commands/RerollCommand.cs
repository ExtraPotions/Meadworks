// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Rolling;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// <c>inscribe reroll [cursor|hover|&lt;slot&gt;]</c> (console-commands.md section 3): rerolls every affix of an item in
    /// the caller's own inventory, keeping its rarity. A test tool: it ignores seals, costs, <c>Modify equipped
    /// items</c> and the rarity's affix count, but never writes an item of a newer format.
    /// <para>
    /// Spec gap, decided here: "ignores the rarity's affix count" is read as <b>keep the item's affix count</b>. Every
    /// inscription (dormant ones included) is removed and the same number is drawn again with
    /// <see cref="ItemRoller.AddAffixes"/>; unreadable segments stay. An item with no inscription is rolled fresh at
    /// its rarity instead (<see cref="ItemRoller.RollFresh"/>).
    /// </para>
    /// </summary>
    internal static class RerollCommand
    {
        public const string Grammar = "inscribe reroll [" + ItemTargets.InventoryWords + "]";

        public static void Run(CommandCall call)
        {
            string word = call.Lower(0);
            if (word.Length > 0 && !ItemTargets.IsWord(word, allowGround: false))
            {
                call.Fail($"unknown target '{word}'.", Grammar);
                return;
            }
            ItemDrop.ItemData? item = ItemTargets.OwnMagicBase(call, word, Grammar);
            ItemState state = ItemState.Read(item);
            if (item == null || !Rerollable(call, state))
            {
                return;
            }
            int count = state.AffixCount;
            ItemState stripped = state.ToBuilder().ClearAffixes().Build();
            RollContext context = RollContext.For(item);
            context.Random = RollRandom.Create();
            ItemState? rolled = count > 0
                ? RollerCall.Roll(call, () => ItemRoller.AddAffixes(stripped, count, context))
                : RollerCall.Roll(call, () => ItemRoller.RollFresh(state, state.Rarity!, context));
            if (rolled != null && RollerCall.Commit(call, item, rolled))
            {
                ItemReport.Write(call, item);
            }
        }

        private static bool Rerollable(CommandCall call, ItemState state)
        {
            string? problem = !state.IsMagic ? "the item is Normal: it has no rarity to keep."
                : state.Rarity == null ? $"the item's rarity '{state.RarityId}' is not in the configuration."
                : null;
            if (problem != null)
            {
                call.Reply(problem);
            }
            return problem == null && !ItemTargets.IsNewer(call, state);
        }
    }
}
