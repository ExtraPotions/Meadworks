// Burrow - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Burrow
{
    /// <summary>
    /// One item taken out of one chest for another product (Kindle's salvage from nearby chests), through the same durable
    /// hold as Shared Materials payments. The answers are the <c>int</c> results of capability <c>meadworks.storage.chest_take</c> 1.0.
    /// </summary>
    public static class ChestItemTake
    {
        /// <summary>The item left the chest and <c>deliver</c> ran once.</summary>
        public const int Taken = 0;
        /// <summary>Nothing changed yet: the chest's owner is handing it over, an inventory is briefly held, or the removal is not on the chest. Ask again.</summary>
        public const int Pending = 1;
        /// <summary>Nothing changed: the slot no longer holds the listed item.</summary>
        public const int Changed = 2;
        /// <summary>Nothing changed: the chest cannot be used now (out of reach, in use, a ward, the ledger is unavailable).</summary>
        public const int Unavailable = 3;

        /// <summary>Marks a chest-salvage record so a later session can tell it from a Shared Materials payment.</summary>
        static readonly byte[] SalvageMetadata = { 1 };

        /// <summary>True when <paramref name="record"/> is a chest salvage from <see cref="Run"/>.</summary>
        public static bool IsSalvage(InventoryDecisionRecord record) => record != null && record.Metadata.Length == 1 && record.Metadata[0] == 1;

        /// <summary>
        /// Prepares a hold on the chest's exact image, decides and applies the removal (the chest is saved with a receipt),
        /// then calls <paramref name="deliver"/> once the removal is on the chest. A save that still has to be retried
        /// delivers, because the gear has already left. A recovery that has not removed the item answers
        /// <see cref="Pending"/> and delivers nothing. A hold that is refused or aborted changes nothing and delivers
        /// nothing. <paramref name="decide"/> runs between the hold and the decision (the plugin marks the chest held
        /// for other clients there).
        /// </summary>
        public static int Run(DurableInventoryParticipant participant, string id, string fingerprint, byte[] before, byte[] after,
            Action? decide, Action deliver, out InventoryDecision state)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (deliver == null) throw new ArgumentNullException(nameof(deliver));
            state = participant.Prepare(id, id, fingerprint, before, after, SalvageMetadata);
            if (state != InventoryDecision.Prepared)
                return Unavailable;
            try
            {
                decide?.Invoke();
            }
            catch (Exception)
            {
                state = participant.Rollback(id);
                return Unavailable;
            }
            state = participant.Commit(id);
            if (state == InventoryDecision.Committed || (state == InventoryDecision.RecoveryRequired && participant.Applied(id)))
            {
                deliver();
                return Taken;
            }
            if (state == InventoryDecision.RecoveryRequired)
                return Pending;
            return state == InventoryDecision.Aborted ? Changed : Unavailable;
        }
    }

    /// <summary>
    /// Pays one chest salvage once, when a later commit actually removes the item. The callback cannot be stored in the
    /// journal, so a new session cancels an unapplied salvage instead of removing the gear with no payment.
    /// </summary>
    public sealed class ChestTakeRecovery
    {
        readonly Dictionary<string, Action> waiting = new Dictionary<string, Action>(StringComparer.Ordinal);

        public string? PendingId
        {
            get { foreach (var pair in waiting) return pair.Key; return null; }
        }

        public bool Waiting(string id) => id != null && waiting.ContainsKey(id);

        public void Clear() => waiting.Clear();

        /// <summary>Remember <paramref name="deliver"/> only while the take is still <see cref="ChestItemTake.Pending"/>.</summary>
        public void Observe(string id, int code, Action deliver)
        {
            if (code == ChestItemTake.Pending && deliver != null)
                waiting[id] = deliver;
            else
                waiting.Remove(id);
        }

        /// <summary>
        /// Calls the delivery once when <paramref name="state"/> has the removal on the chest. <paramref name="prefer"/>
        /// is the caller waiting right now; otherwise the remembered callback runs.
        /// </summary>
        public bool Pay(DurableInventoryParticipant participant, string id, InventoryDecision state, Action? prefer = null)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            if (!waiting.TryGetValue(id, out var stored))
                return false;
            if (state != InventoryDecision.Committed && !(state == InventoryDecision.RecoveryRequired && participant.Applied(id)))
                return false;
            waiting.Remove(id);
            (prefer ?? stored)();
            return true;
        }
    }
}
