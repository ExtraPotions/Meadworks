// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: what the shared-materials provider answers when it cannot run a reservation (playtest 2026-10-20).
using ExtraPotions.Meadworks.Contracts;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// The meaning of the provider's answers, within the unchanged Crucible contract:
    /// <list type="bullet">
    /// <item><c>Failed</c> (no reservation): definite. Burrow holds nothing for the request and charged nothing.</item>
    /// <item><c>RecoveryRequired</c> (with a reservation): Burrow cannot tell right now (its ledger is not open, or a record
    /// for the request exists and needs reconciling). The caller polls <c>GetStatusAsync</c>; once Burrow is available the
    /// status settles to Reserved, Committed, RolledBack or Failed.</item>
    /// <item>Temporary conditions while the ledger is open and holds no record (player dead or teleporting, inventory held
    /// by a transfer, stock held by another hold) wait inside Burrow up to the request's ten seconds, then answer Failed.</item>
    /// </list>
    /// </summary>
    public static class ReservationAnswers
    {
        public enum Answer { Rejected, Unknown }

        /// <summary>
        /// A request that could not be queued (the bounded queue is full, or the provider is shutting down): rejected when
        /// the ledger is open and has no record under the request's id; unknown when the ledger is closed (it may hold one).
        /// </summary>
        public static Answer NotQueued(bool ledgerOpen, bool recorded) => ledgerOpen && !recorded ? Answer.Rejected : Answer.Unknown;

        /// <summary>The ledger cannot be read now (Crucible or the world authority not ready, no player, journal fault).</summary>
        public static Answer Unavailable() => Answer.Unknown;

        public static AsyncReservationState State(Answer answer) =>
            answer == Answer.Rejected ? AsyncReservationState.Failed : AsyncReservationState.RecoveryRequired;
    }
}
