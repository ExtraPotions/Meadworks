// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using ExtraPotions.Inscribe.Adopted.Affixes;

namespace ExtraPotions.Inscribe.Adopted.Rolling
{
    /// <summary>The result of a roll: the new state (not yet written) or why there is none.</summary>
    public readonly struct RollOutcome
    {
        private RollOutcome(ItemState? state, RollFailure failure)
        {
            State = state;
            Failure = failure;
        }

        /// <summary>The rolled state; hand it to <see cref="ItemState.Write"/> to commit. Null on failure.</summary>
        public ItemState? State { get; }

        public RollFailure Failure { get; }

        public bool Success => Failure == RollFailure.None && State != null;

        public static RollOutcome Ok(ItemState state) => new RollOutcome(state, RollFailure.None);

        public static RollOutcome Fail(RollFailure failure) => new RollOutcome(null, failure);
    }
}
