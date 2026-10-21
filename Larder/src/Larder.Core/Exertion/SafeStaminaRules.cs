// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Behavior specification only: jekkle/cartur-safe-stamina at 2960c141b64e55852c0d1b423c8d82e64e48555c.
// No source copied: that inspected repository has no licence grant. Native game seams supply all state.
using System;

namespace ExtraPotions.Larder.Exertion
{
    [Flags]
    public enum StaminaBlock
    {
        None = 0, Swimming = 1, Attacking = 2, Dodging = 4, WallRunning = 8, Encumbered = 16
    }

    public static class SafeStaminaRules
    {
        public static bool AllowsBlockedRegeneration(StaminaBlock state, bool swimming, bool attacks)
            => AllowsBlockedRegeneration(state, swimming, attacks, false);

        /// <summary>Only blocked frames; encumbrance requires its own opt-in as well as the other state's switch.</summary>
        public static bool AllowsBlockedRegeneration(StaminaBlock state, bool swimming, bool attacks, bool encumbered)
        {
            bool loaded = (state & StaminaBlock.Encumbered) != 0;
            if (loaded && !encumbered) return false;
            // Swimming owns the decision while swimming, including a simultaneous swing/dodge.
            if ((state & StaminaBlock.Swimming) != 0) return swimming;
            if ((state & (StaminaBlock.Attacking | StaminaBlock.Dodging)) != 0) return attacks;
            return loaded || (state & StaminaBlock.WallRunning) != 0;
        }
    }
}
