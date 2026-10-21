// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Notifier.cs Postfix: the clock start it keeps in memory and when it tells the opener). The MIT notice is in
// THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: a pure decision over what one chest update shows (item count, clock, open, new opening);
// the plugin keeps the seen times and the per-opening deduplication; texts are Delve catalog keys. OpenerOf adds the
// authority's own player through a local hook (user decision R-Q4, 2026-10-08), which RestockRegen does not see.
using ExtraPotions.Delve.Localization;

namespace ExtraPotions.Delve.Chests
{
    /// <summary>Who has the chest open: a connected remote player, the authority's own player (single player or host), or nobody to tell.</summary>
    public enum ChestOpener { None, Remote, Local }

    /// <summary>What happens to the chest's seen time (the clock start before the sweep stamps it).</summary>
    public enum SeenChange { None, Start, Clear }

    public sealed class ChestObservationResult
    {
        internal ChestObservationResult(SeenChange seen, bool tellHistory, string? noticeKey)
        {
            Seen = seen;
            TellHistory = tellHistory;
            NoticeKey = noticeKey;
        }

        public SeenChange Seen { get; }

        /// <summary>A new opening: record it in the history and tell the opener who came before.</summary>
        public bool TellHistory { get; }

        /// <summary>The refill notice for the opener (refill or leftovers); <see cref="ChestObservation.Key"/> turns it into "due" when no time is left.</summary>
        public string? NoticeKey { get; }
    }

    /// <summary>
    /// A chest update reaches the authority when the player who opened it (and owns it while it is open) sends it. After
    /// that update: "in use" means that player has it open; empty with no clock starts the clock; with Reset Opened a new
    /// opening starts it, leftovers or not; without Reset Opened anything inside stops it.
    /// </summary>
    public static class ChestObservation
    {
        public static ChestObservationResult Observe(int? count, bool clocked, bool seen, bool open, bool newOpening, bool resetOpened)
        {
            bool opening = open && newOpening;
            if (count == null) return new ChestObservationResult(SeenChange.None, opening, null);
            bool empty = count.Value == 0;
            bool unclocked = !clocked && !seen;
            var change = SeenChange.None;
            if (empty && unclocked) change = SeenChange.Start;
            else if (!empty && !resetOpened) change = SeenChange.Clear;
            else if (opening && resetOpened && unclocked) change = SeenChange.Start;
            string? notice = open && (empty || resetOpened) ? Key(empty, 1) : null;
            return new ChestObservationResult(change, opening, notice);
        }

        /// <summary>
        /// The player to tell. A remote player owns the chest while they have it open, and their update reaches the
        /// authority; the authority's own player changes the chest locally, which only the local hook sees.
        /// </summary>
        public static ChestOpener OpenerOf(bool open, long owner, long session, bool ownerConnected, bool localHook, bool localPlayer)
        {
            if (!open || owner == 0L) return ChestOpener.None;
            if (owner != session) return ownerConnected && !localHook ? ChestOpener.Remote : ChestOpener.None;
            return localHook && localPlayer ? ChestOpener.Local : ChestOpener.None;
        }

        public static string Key(bool empty, int unitsLeft) =>
            unitsLeft <= 0 ? DelveTextKeys.ChestDue : empty ? DelveTextKeys.ChestRefill : DelveTextKeys.ChestRefillLeftovers;
    }
}
