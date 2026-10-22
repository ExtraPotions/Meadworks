// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Barter.Trading
{
    /// <summary>Only a known inventory outcome may be retried after reconnecting as the same character.</summary>
    public sealed class TradeRecovery
    {
        private readonly long world;
        private readonly string actor;
        private bool awaitingReservation = true, inFlight;
        private double retryAt;
        public bool? Outcome { get; private set; } = false;
        public bool Done { get; private set; }

        public TradeRecovery(long world, string actor)
        {
            if (world == 0 || string.IsNullOrEmpty(actor)) throw new ArgumentException("Missing recovery identity.");
            this.world = world; this.actor = actor;
        }

        public void ReservationEnded() { awaitingReservation = false; }
        public void InventoryStarting() { awaitingReservation = false; Outcome = null; }
        public void InventoryFinished(bool success) { awaitingReservation = false; Outcome = success; }
        public bool TryDispatch(long currentWorld, string currentActor, double now)
        {
            if (Done || awaitingReservation || inFlight || !Outcome.HasValue || currentWorld != world ||
                currentActor != actor || now < retryAt) return false;
            inFlight = true; retryAt = now + 2;
            return true;
        }
        public void Reply(bool acknowledged) { inFlight = false; Done |= acknowledged; }
        public void Suspend() { awaitingReservation = false; inFlight = false; }

        public static bool CanConfirm(JournalEntry entry, string loadedInventory)
            => entry.Phase == 2 && entry.AfterInventory.Length > 0 && entry.AfterInventory == loadedInventory;
    }
}
