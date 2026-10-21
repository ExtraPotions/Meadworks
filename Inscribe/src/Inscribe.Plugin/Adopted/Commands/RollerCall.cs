// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe command identity and live permission gates.
using System;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;
using ExtraPotions.Inscribe.Adopted.Rolling;

namespace ExtraPotions.Inscribe.Adopted.Commands
{
    /// <summary>
    /// Calls into the shared roller (<c>Rolling/ItemRoller</c>, implemented by the Stones area) for the test commands,
    /// and commits the result. A roller that is still a stub throws <see cref="NotImplementedException"/>; that is
    /// reported to the console instead of failing the command. Writes go through <see cref="ItemState.Write"/>, the only
    /// write path, on the caller's own item.
    /// </summary>
    internal static class RollerCall
    {
        /// <summary>The rolled state, or null after replying why there is none.</summary>
        public static ItemState? Roll(CommandCall call, Func<RollOutcome> roll)
        {
            RollOutcome outcome;
            try
            {
                outcome = roll();
            }
            catch (NotImplementedException)
            {
                call.Reply("not implemented yet: the item roller (Rolling/ItemRoller) is still a stub.");
                return null;
            }
            if (!outcome.Success)
            {
                call.Reply($"the roll failed: {EnumIds<RollFailure>.Id(outcome.Failure)}.");
                return null;
            }
            return outcome.State;
        }

        /// <summary>Writes the state and logs it when <c>Log rolls</c> is on; false after replying when refused.</summary>
        public static bool Commit(CommandCall call, ItemDrop.ItemData item, ItemState state)
        {
            bool written;
            using (ItemChanges.Because(ItemChanges.Command))
            {
                written = ItemState.Write(item, state);
            }
            if (!written)
            {
                call.Reply("the item refused the new state (see the log).");
                return false;
            }
            if (ModSettings.LogRolls != null && ModSettings.LogRolls.Value)
            {
                Log.Info($"roll (inscribe {call.Sub}): {ItemText.Prefab(item)} -> {ItemReport.Raw(item.m_customData)}");
            }
            return true;
        }
    }
}
