// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// MeadWorks-original: bounded history for the requester's theft journal (post-release cleanup, 2026-10-20).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtraPotions.Wildblood.Transfers
{
    /// <summary>What one cleanup pass removed. <see cref="ReceiptRoots"/> lists the roots whose character receipt keys the caller
    /// removes; <see cref="Pruned"/> lists the requests removed in this pass (their world rows may still be released).</summary>
    public sealed class TheftCleanupResult
    {
        public static readonly TheftCleanupResult None = new TheftCleanupResult(Array.Empty<TheftRequest>(), Array.Empty<string>(), 0, 0, null);
        public TheftCleanupResult(IReadOnlyList<TheftRequest> pruned, IReadOnlyList<string> receiptRoots, int rows, int kept, string? skipped, string? error = null)
        { Pruned = pruned; ReceiptRoots = receiptRoots; Rows = rows; Kept = kept; Skipped = skipped; Error = error; }
        public IReadOnlyList<TheftRequest> Pruned { get; }
        public IReadOnlyList<string> ReceiptRoots { get; }
        /// <summary>Records removed across the four journals.</summary>
        public int Rows { get; }
        /// <summary>Settled thefts kept as recent history.</summary>
        public int Kept { get; }
        /// <summary>Why nothing was pruned (an unsettled or unconfirmed theft), or null.</summary>
        public string? Skipped { get; }
        /// <summary>The exception type that stopped this pass after the folder was recovered, or null.</summary>
        public string? Error { get; }
    }

    /// <summary>
    /// Removes old settled thefts from the requester's journal folder so it never fills (each journal admits 4096 records).
    /// A theft is removable only when its intent, outcome, debit and settlement are all Committed and both saved character
    /// receipts are present. Any record in any journal that is not Committed, or any theft without its receipts, means
    /// recovery may still need the history, and then no journal is rewritten.
    /// <para>Order makes an interrupted pass safe. The roots are written to a manifest first, then the intent journal is
    /// rewritten, then the three phase journals, each by a verified copy swapped in with <c>File.Replace</c>. Receipt keys
    /// are removed by the caller only after the journals no longer hold those roots: a receipt removed while its Committed row
    /// remained would make reconciliation treat the row as unsaved. The manifest keeps the roots until a later pass sees the
    /// loaded character without their keys.</para>
    /// Runs before <see cref="TheftClientLedger"/> opens the folder, so no transfer is in flight.
    /// </summary>
    public static class TheftJournalCleanup
    {
        /// <summary>Newest settled thefts kept; they also keep the highest ask counter, so counters are never reused.</summary>
        public const int KeepRecent = 32;
        /// <summary>Pruning starts above this many thefts, so the files are not rewritten on every load.</summary>
        public const int Trigger = 64;
        public const string Manifest = "cleanup.pending";
        const string Copy = ".cleanup-new", Backup = ".cleanup-old";
        static readonly string[] Journals = { "intent.bin", "outcome.bin", "debit.bin", "settlement.bin" };

        /// <summary>Receipt sources written by <see cref="TheftClientLedger"/> for one theft.</summary>
        public static string DebitSource(string root) => "wildblood.theft.debit." + root;
        public static string SettleSource(string root) => "wildblood.theft.settle." + root;
        public static IEnumerable<string> Sources(string root) { yield return DebitSource(root); yield return SettleSource(root); }

        /// <summary>Restores a journal whose swap was interrupted and removes leftover copies. Throws when the folder cannot be
        /// made consistent; the caller must then not open the ledger, which would create an empty journal in its place.</summary>
        public static void Recover(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (string name in Journals)
            {
                string path = Path.Combine(directory, name), old = path + Backup, copy = path + Copy;
                if (File.Exists(old))
                {
                    if (File.Exists(path)) File.Delete(old); // the swap finished; the old file is no longer needed
                    else File.Move(old, path);              // the swap stopped half-way: the original comes back
                }
                if (File.Exists(copy)) File.Delete(copy);
            }
            string manifestCopy = Path.Combine(directory, Manifest) + Copy;
            if (File.Exists(manifestCopy)) File.Delete(manifestCopy);
        }

        /// <summary>One pass: <see cref="Recover"/>, then <see cref="Prune"/>. A pruning failure leaves the folder recovered and
        /// reports nothing removed; only a failed recovery throws.</summary>
        /// <param name="receiptSaved">True when the loaded character holds the saved receipt for a source.</param>
        public static TheftCleanupResult Run(string directory, Func<string, bool> receiptSaved)
        {
            Recover(directory);
            try { return Prune(directory, receiptSaved, KeepRecent, Trigger, null); }
            catch (Exception error)
            {
                Recover(directory);
                return new TheftCleanupResult(Array.Empty<TheftRequest>(), Array.Empty<string>(), 0, 0, null, error.GetType().Name);
            }
        }

        /// <summary>The pass itself. <paramref name="step"/> is called after each durable step (a test seam: an exception thrown
        /// there stands for a crash at that point and propagates unhandled).</summary>
        public static TheftCleanupResult Prune(string directory, Func<string, bool> receiptSaved, int keep, int trigger, Action<string>? step)
        {
            if (keep < 1 || trigger < keep) throw new ArgumentOutOfRangeException(nameof(trigger));
            if (!File.Exists(Path.Combine(directory, Journals[0]))) return TheftCleanupResult.None;
            var records = Journals.ToDictionary(n => n, n => Read(Path.Combine(directory, n)));
            var intents = records[Journals[0]].Select(r => new TheftIntent(r)).ToList();
            var present = new HashSet<string>(records.Values.SelectMany(l => l.Select(r => r.Id)), StringComparer.Ordinal);

            // Manifest roots no longer in any journal and no longer on the loaded character are finished.
            var pending = ReadManifest(directory).Where(root => present.Contains(root) || Sources(root).Any(receiptSaved)).ToList();

            string? skipped = null;
            if (records.Values.SelectMany(l => l).Any(r => r.State != InventoryDecision.Committed)) skipped = "unsettled";
            else
            {
                var phases = records.Where(p => p.Key != Journals[0]).Select(p => p.Value).ToList();
                foreach (var intent in intents)
                    if (phases.Any(l => l.All(r => r.Id != intent.Root)) || !Sources(intent.Root).All(receiptSaved)) { skipped = "unconfirmed"; break; }
            }

            var prune = new List<TheftIntent>();
            var remove = new HashSet<string>(StringComparer.Ordinal);
            if (skipped == null)
            {
                // Everything is settled and confirmed: drop the oldest beyond the recent history, finish the roots an
                // interrupted pass already named, and drop phase rows whose intent that pass already removed.
                var oldest = intents.Count > trigger ? intents.OrderByDescending(i => i.Request.Counter).Skip(keep) : Enumerable.Empty<TheftIntent>();
                prune = oldest.Concat(intents.Where(i => pending.Contains(i.Root))).Distinct().ToList();
                remove.UnionWith(prune.Select(i => i.Root));
                var known = new HashSet<string>(intents.Select(i => i.Root), StringComparer.Ordinal);
                remove.UnionWith(present.Where(id => !known.Contains(id)));
            }
            var roots = pending.Concat(remove).Distinct(StringComparer.Ordinal).ToList();
            // Receipt keys may go only for roots that no journal holds once this pass is done.
            var receipts = roots.Where(r => !present.Contains(r) || remove.Contains(r)).ToList();
            WriteManifest(directory, roots);
            if (remove.Count == 0)
                return roots.Count == 0 && skipped == null ? TheftCleanupResult.None
                    : new TheftCleanupResult(Array.Empty<TheftRequest>(), receipts, 0, intents.Count, skipped);
            step?.Invoke("manifest");

            int rows = 0;
            foreach (string name in Journals) rows += Compact(directory, name, records[name], remove, step);
            return new TheftCleanupResult(prune.Select(i => i.Request).ToList(), receipts, rows, intents.Count - prune.Count, null);
        }

        static IReadOnlyList<InventoryDecisionRecord> Read(string path)
        {
            if (!File.Exists(path)) return Array.Empty<InventoryDecisionRecord>();
            using (var journal = new InventoryDecisionJournal(path, true)) return journal.Snapshot();
        }

        static int Compact(string directory, string name, IReadOnlyList<InventoryDecisionRecord> all, HashSet<string> remove, Action<string>? step)
        {
            var keep = all.Where(r => !remove.Contains(r.Id)).ToList();
            if (keep.Count == all.Count) return 0;
            string path = Path.Combine(directory, name), copy = path + Copy, old = path + Backup;
            if (File.Exists(copy)) File.Delete(copy);
            using (var target = new InventoryDecisionJournal(copy))
                foreach (var record in keep)
                {
                    var written = target.Prepare(record.Id, record.Source, record.Fingerprint, record.Deadline, record.Before, record.After, record.Metadata);
                    foreach (var next in Steps(record.State)) target.Transition(written, next);
                }
            using (var check = new InventoryDecisionJournal(copy, true))
            {
                var copied = check.Snapshot();
                if (copied.Count != keep.Count || keep.Any(r => !Same(r, check.Find(r.Id))))
                    throw new InvalidDataException("Theft journal copy differs from the kept records.");
            }
            step?.Invoke(name + ".copied");
            File.Replace(copy, path, old);
            step?.Invoke(name + ".swapped");
            File.Delete(old);
            return all.Count - keep.Count;
        }

        static IEnumerable<InventoryDecision> Steps(InventoryDecision state)
        {
            switch (state)
            {
                case InventoryDecision.Decided: yield return InventoryDecision.Decided; break;
                case InventoryDecision.Committed: yield return InventoryDecision.Decided; yield return InventoryDecision.Committed; break;
                case InventoryDecision.Aborted: yield return InventoryDecision.Aborted; break;
            }
        }

        static bool Same(InventoryDecisionRecord a, InventoryDecisionRecord? b) =>
            b != null && a.Id == b.Id && a.Source == b.Source && a.Fingerprint == b.Fingerprint && a.Deadline.Equals(b.Deadline) && a.State == b.State
            && a.Before.SequenceEqual(b.Before) && a.After.SequenceEqual(b.After) && a.Metadata.SequenceEqual(b.Metadata);

        // A manifest that cannot be read is dropped: it only names receipt keys to tidy, and forgetting them leaves a few
        // unused keys on the character, never a missing receipt.
        static List<string> ReadManifest(string directory)
        {
            string path = Path.Combine(directory, Manifest);
            if (!File.Exists(path)) return new List<string>();
            try
            {
                var roots = File.ReadAllLines(path).Where(l => l.Length != 0).ToList();
                return roots.All(TheftWire.IsRoot) ? roots.Distinct(StringComparer.Ordinal).ToList() : new List<string>();
            }
            catch (IOException) { return new List<string>(); }
        }

        static void WriteManifest(string directory, IReadOnlyCollection<string> roots)
        {
            string path = Path.Combine(directory, Manifest);
            if (roots.Count == 0) { if (File.Exists(path)) File.Delete(path); return; }
            string copy = path + Copy;
            using (var stream = new FileStream(copy, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                foreach (string root in roots) writer.WriteLine(root);
                writer.Flush();
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(copy, path, null);
            else File.Move(copy, path);
        }
    }
}
