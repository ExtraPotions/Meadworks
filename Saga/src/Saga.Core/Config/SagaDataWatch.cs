// Saga - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: change detection for the YAML families, so the main thread reads the files only after they changed.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ExtraPotions.Saga.Config
{
    /// <summary>
    /// Counts changes to the Saga YAML files in one folder. A file watcher and a slow metadata re-check (both off the main
    /// thread) bump <see cref="Generation"/>; a reader rescans when the generation moved and <see cref="Settled"/> holds.
    /// </summary>
    public sealed class SagaDataWatch : IDisposable
    {
        public const int DebounceMs = 500, SafetyMs = 10000;
        readonly string root;
        readonly (DataFamily Family, string Pattern)[] sets;
        int generation = 1, lastChange = Environment.TickCount;
        string? signature;
        FileSystemWatcher? watcher;
        Timer? safety;

        public SagaDataWatch(string root, IEnumerable<(DataFamily Family, string Pattern)> sets)
        {
            this.root = root;
            this.sets = sets.ToArray();
        }

        public int Generation => Volatile.Read(ref generation);

        /// <summary>True once no change was seen for <see cref="DebounceMs"/>, so an editor's save has finished.</summary>
        public bool Settled(int now) => unchecked(now - Volatile.Read(ref lastChange)) >= DebounceMs;

        public void Touch(int now)
        {
            Volatile.Write(ref lastChange, now);
            Interlocked.Increment(ref generation);
        }

        /// <summary>Starts the watcher and the safety re-check. Without a working watcher the re-check alone still notices changes.</summary>
        public void Start()
        {
            try
            {
                watcher = new FileSystemWatcher(root) { IncludeSubdirectories = false, NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime };
                FileSystemEventHandler changed = (_, e) => { if (Relevant(e.Name)) Touch(Environment.TickCount); };
                watcher.Changed += changed; watcher.Created += changed; watcher.Deleted += changed;
                watcher.Renamed += (_, e) => { if (Relevant(e.Name) || Relevant(e.OldName)) Touch(Environment.TickCount); };
                watcher.Error += (_, _) => Touch(Environment.TickCount);
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception) { watcher?.Dispose(); watcher = null; }
            safety = new Timer(_ => Check(), null, 0, SafetyMs);
        }

        /// <summary>Compares the files' names, sizes and write times with the previous check; a difference counts as a change.</summary>
        public void Check()
        {
            try
            {
                var next = Snapshot();
                if (next != signature) { if (signature != null) Touch(Environment.TickCount); signature = next; }
            }
            catch (Exception) { Touch(Environment.TickCount); }
        }

        /// <summary>The same files a reader picks up (pattern listing, then <see cref="SagaDataFiles.Layers"/>), with their metadata.</summary>
        public string Snapshot()
        {
            var text = new StringBuilder();
            foreach (var set in sets)
                foreach (var path in SagaDataFiles.Layers(set.Family, Directory.GetFiles(root, set.Pattern)))
                {
                    var file = new FileInfo(path);
                    text.Append(path).Append('|').Append(file.Exists ? file.Length : -1).Append('|').Append(file.Exists ? file.LastWriteTimeUtc.Ticks : 0).Append('\n');
                }
            return text.ToString();
        }

        public static bool Relevant(string? name) => SagaDataFiles.Classify(name) != null;

        public void Dispose()
        {
            safety?.Dispose(); safety = null;
            watcher?.Dispose(); watcher = null;
        }
    }
}
