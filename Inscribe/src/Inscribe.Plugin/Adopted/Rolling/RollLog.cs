// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Text;
using ExtraPotions.Inscribe.Adopted.Affixes;
using ExtraPotions.Inscribe.Adopted.Config;
using ExtraPotions.Inscribe.Adopted.Core;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>
    /// The <c>Log rolls</c> diagnostic (configuration.md section 2, per player): every roll through
    /// <see cref="ItemRoller"/> with its inputs and result, whoever asked for it (stone, drop, command). Costs one
    /// setting read when off.
    /// </summary>
    internal static class RollLog
    {
        public static RollOutcome Report(string what, ItemState before, RollOutcome outcome, RollContext context)
        {
            if (ModSettings.LogRolls == null || !ModSettings.LogRolls.Value)
            {
                return outcome;
            }
            string inputs = $"class {context.Class.ClassId ?? "none"}, level {context.Level}, floor {context.TierFloor}"
                + (context.Chaotic ? ", chaotic" : "") + (context.LimitOverflow > 0 ? $", overflow {context.LimitOverflow}" : "");
            string result = outcome.Success ? Describe(outcome.State!) : "failed: " + outcome.Failure;
            Log.Info($"roll {what} ({inputs}): {Describe(before)} -> {result}");
            return outcome;
        }

        private static string Describe(ItemState state)
        {
            StringBuilder text = new StringBuilder(state.RarityId ?? "normal");
            text.Append(" [");
            for (int i = 0; i < state.AffixCount; i++)
            {
                text.Append(i == 0 ? "" : ", ").Append(state.Affixes[i]);
            }
            return text.Append(']').ToString();
        }
    }
}
