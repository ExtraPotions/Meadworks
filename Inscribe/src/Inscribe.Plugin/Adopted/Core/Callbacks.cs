// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Inscribe.Adopted.Core
{
    /// <summary>
    /// Callbacks other mods hand in through the public API (api.md section 1): kept by id (a registration replaces the
    /// one with its id: providers, filters) or by delegate (listeners, whose source is the declaring assembly's name).
    /// Read through a copy-on-write snapshot, so a callback may unregister itself while the list runs. A foreign callback
    /// that throws is reported through <see cref="Failed"/>: logged once per kind and source, and the caller treats it as
    /// no answer. Main thread only.
    /// </summary>
    internal sealed class Callbacks<T> where T : Delegate
    {
        private readonly string _what;
        private Entry[] _entries = Array.Empty<Entry>();

        public Callbacks(string what)
        {
            _what = what;
        }

        /// <summary>The registered callbacks in registration order; never changed in place.</summary>
        public Entry[] Snapshot => _entries;

        public bool IsEmpty => _entries.Length == 0;

        /// <summary>Adds a callback, or replaces the one with this id (it keeps its place). False for an empty id or callback.</summary>
        public bool Set(string? id, T? callback)
        {
            if (string.IsNullOrEmpty(id) || callback == null)
            {
                return false;
            }
            List<Entry> list = new List<Entry>(_entries);
            int index = list.FindIndex(e => e.Id == id);
            Entry entry = new Entry(id!, id!, callback);
            if (index >= 0)
            {
                list[index] = entry;
            }
            else
            {
                list.Add(entry);
            }
            _entries = list.ToArray();
            return true;
        }

        /// <summary>Removes the callback with this id. False when there is none.</summary>
        public bool Remove(string? id)
        {
            List<Entry> list = new List<Entry>(_entries);
            bool removed = id != null && list.RemoveAll(e => e.Id == id) > 0;
            _entries = list.ToArray();
            return removed;
        }

        /// <summary>Adds a listener; false when it is null or already added.</summary>
        public bool AddListener(T? callback)
        {
            if (callback == null || Array.Exists(_entries, e => e.Callback.Equals(callback)))
            {
                return false;
            }
            string source = SourceOf(callback);
            List<Entry> list = new List<Entry>(_entries) { new Entry(source + "#" + callback.GetHashCode(), source, callback) };
            _entries = list.ToArray();
            return true;
        }

        /// <summary>Removes a listener added before (the same delegate). False when it was not there.</summary>
        public bool RemoveListener(T? callback)
        {
            List<Entry> list = new List<Entry>(_entries);
            bool removed = callback != null && list.RemoveAll(e => e.Callback.Equals(callback)) > 0;
            _entries = list.ToArray();
            return removed;
        }

        /// <summary>A callback threw: logged once per kind and source; the caller counts it as no answer.</summary>
        public void Failed(Entry entry, Exception e) => ForeignFailures.Report(_what, entry.Source, e);

        private static string SourceOf(Delegate callback) =>
            callback.Method?.DeclaringType?.Assembly.GetName().Name ?? "an unknown mod";

        /// <summary>One registered callback: its id, the source named in the log, the callback.</summary>
        public sealed class Entry
        {
            public Entry(string id, string source, T callback)
            {
                Id = id;
                Source = source;
                Callback = callback;
            }

            public string Id { get; }
            public string Source { get; }
            public T Callback { get; }
        }
    }

    /// <summary>The one place a foreign callback's failure is logged: once per kind and source, never again.</summary>
    internal static class ForeignFailures
    {
        private static readonly HashSet<string> Reported = new HashSet<string>(StringComparer.Ordinal);

        public static void Report(string what, string source, Exception e)
        {
            if (Reported.Add(what + "|" + source))
            {
                Log.Warn($"{what} '{source}' threw; each failure counts as no answer (logged once): {e}");
            }
        }
    }
}
