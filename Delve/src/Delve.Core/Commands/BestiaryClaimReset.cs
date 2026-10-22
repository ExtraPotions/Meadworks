// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: a full regenerate asks Bestiary to clear region claims after it finishes. Bestiary owns the keys.
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Commands
{
    /// <summary>
    /// <c>meadworks.bestiary.world</c> v1. A regenerate that names every kind may clear Rime Giant and Slag Charred
    /// claims once that regenerate has finished. One kind does not. Bestiary removes the keys; Delve decides when to ask.
    /// </summary>
    public static class BestiaryClaimReset
    {
        public const string CapabilityId = "meadworks.bestiary.world";
        public static readonly Version Version = new Version(1, 0);

        public static bool Clears(DelveCommand command) =>
            command.Verb == CommandVerb.Regenerate && CoversEveryKind(command.Kinds);

        /// <summary>
        /// The clear may be armed. The ask names every kind, the delay is skipped, Dry Run All is off, and at least
        /// one kind was accepted. A request that keeps the usual delays is not a regenerate.
        /// </summary>
        public static bool Arms(bool everyKind, bool dryRun, bool skipDelay, int acceptedCount) =>
            everyKind && !dryRun && skipDelay && acceptedCount > 0;

        public static bool CoversEveryKind(IEnumerable<DelveKind> kinds)
        {
            var seen = new HashSet<DelveKind>();
            foreach (var kind in kinds) seen.Add(kind);
            return seen.Count == Enum.GetValues(typeof(DelveKind)).Length;
        }

        public static int Apply(Func<int> reset, bool clear)
        {
            if (!clear || reset == null) return 0;
            return reset();
        }
    }

    /// <summary>
    /// One world-wide claim clear. Delve sweeps kinds for the whole world, not one Bestiary region at a time, and the
    /// v1 contract clears every region claim in one call. The clear is armed when a full regenerate is accepted with
    /// the delay skipped. It fires once when every accepted kind has finished a sweep with nothing deferred and no
    /// failure on that check. A request that keeps the usual delays is not armed.
    /// </summary>
    public sealed class ClaimClearWatch
    {
        private readonly HashSet<DelveKind> pending = new HashSet<DelveKind>();
        private readonly HashSet<DelveKind> failed = new HashSet<DelveKind>();

        /// <summary>
        /// Arms a clear for <paramref name="accepted"/> when <see cref="BestiaryClaimReset.Arms"/> is true. A refusal,
        /// a dry run, or a request that keeps the usual delays leaves an already armed job alone and clears nothing.
        /// </summary>
        public bool Request(bool everyKind, bool dryRun, bool skipDelay, IReadOnlyCollection<DelveKind> accepted)
        {
            if (accepted == null || !BestiaryClaimReset.Arms(everyKind, dryRun, skipDelay, accepted.Count)) return false;
            pending.Clear();
            failed.Clear();
            foreach (var kind in accepted) pending.Add(kind);
            return pending.Count > 0;
        }

        /// <summary>A new engine check. A failure noted on the previous check does not stick.</summary>
        public void BeginCheck() => failed.Clear();

        /// <summary>This check did not finish <paramref name="kind"/>. The kind stays armed for a later sweep.</summary>
        public void NoteFailure(DelveKind kind)
        {
            if (pending.Contains(kind)) failed.Add(kind);
        }

        /// <summary>The world stopped. The armed clear is dropped and nothing is cleared.</summary>
        public void Cancel()
        {
            pending.Clear();
            failed.Clear();
        }

        /// <summary>
        /// The sweep of <paramref name="kind"/> ended. True only the first time the last accepted kind finishes
        /// with nothing deferred and no failure on this check.
        /// </summary>
        public bool NoteSwept(DelveKind kind, int deferred)
        {
            if (!pending.Contains(kind) || deferred > 0 || failed.Contains(kind)) return false;
            pending.Remove(kind);
            return pending.Count == 0;
        }
    }
}
