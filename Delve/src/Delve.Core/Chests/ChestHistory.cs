// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/History.cs Record, Opened, Message and Ago). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: records are immutable values the plugin keeps pending and flushes to the chest's ZDO; the
// message templates and the "you", "today", "yesterday" and "N days ago" words come from Delve's C05 catalog.
using System;

namespace ExtraPotions.Delve.Chests
{
    /// <summary>The words of a history line in one language (Delve catalog keys history_*).</summary>
    public sealed class HistoryWords
    {
        public HistoryWords(string many, string once, string you, string today, string yesterday, string daysAgo)
        {
            Many = many ?? throw new ArgumentNullException(nameof(many));
            Once = once ?? throw new ArgumentNullException(nameof(once));
            You = you ?? throw new ArgumentNullException(nameof(you));
            Today = today ?? throw new ArgumentNullException(nameof(today));
            Yesterday = yesterday ?? throw new ArgumentNullException(nameof(yesterday));
            DaysAgo = daysAgo ?? throw new ArgumentNullException(nameof(daysAgo));
        }

        /// <summary>Template with {last}, {lastago}, {first}, {firstago}.</summary>
        public string Many { get; }

        /// <summary>Template with {last}, {lastago}, when one player opened it on one day.</summary>
        public string Once { get; }

        public string You { get; }
        public string Today { get; }
        public string Yesterday { get; }

        /// <summary>Format with {0} = days.</summary>
        public string DaysAgo { get; }
    }

    /// <summary>
    /// Who first opened a loot chest and who opened it last, with the in-game day, so the next visitor can be told who was
    /// there before them. Stored on the chest's own ZDO, but never written while a player holds the chest (the server
    /// would drop the player's next change to it); the plugin keeps the opening pending until the chest is free.
    /// </summary>
    public static class ChestHistory
    {
        public sealed class Record
        {
            public Record(string firstBy, int firstDay, string lastBy, int lastDay)
            {
                FirstBy = firstBy ?? "";
                FirstDay = firstDay;
                LastBy = lastBy ?? "";
                LastDay = lastDay;
            }

            public static Record None { get; } = new Record("", -1, "", -1);

            public string FirstBy { get; }
            public int FirstDay { get; }
            public string LastBy { get; }
            public int LastDay { get; }
            public bool Empty => LastBy.Length == 0;
        }

        /// <summary>Days are stored as day + 1 so that 0 means "never".</summary>
        public static int Encode(int day) => day < 0 ? 0 : day + 1;

        public static int Decode(int stored) => stored <= 0 ? -1 : stored - 1;

        /// <summary>The record after <paramref name="player"/> opened the chest on <paramref name="today"/>.</summary>
        public static Record Opened(Record before, string player, int today)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            bool known = before.FirstBy.Length > 0;
            return new Record(known ? before.FirstBy : player, known ? before.FirstDay : today, player, today);
        }

        /// <summary>The message for the player opening the chest, from the record before their opening; null when nobody was recorded.</summary>
        public static string? Message(Record before, string player, int today, HistoryWords words)
        {
            if (before == null) throw new ArgumentNullException(nameof(before));
            if (words == null) throw new ArgumentNullException(nameof(words));
            if (before.Empty) return null;
            string last = before.LastBy == player ? words.You : before.LastBy;
            string first = before.FirstBy == player ? words.You : before.FirstBy;
            bool once = before.FirstBy == before.LastBy && before.FirstDay == before.LastDay;
            string template = once ? words.Once : words.Many;
            return template
                .Replace("{last}", last).Replace("{lastago}", Ago(today - before.LastDay, words))
                .Replace("{first}", first).Replace("{firstago}", Ago(today - before.FirstDay, words));
        }

        /// <summary>
        /// The history line to tell the opener: <see cref="Message"/>, or null while Notices / Notify is off (it silences every
        /// Delve notice). The opening is recorded with <see cref="Opened"/> either way.
        /// </summary>
        public static string? Notice(Record before, string player, int today, HistoryWords words, bool notify) =>
            notify ? Message(before, player, today, words) : null;

        private static string Ago(int days, HistoryWords words)
        {
            if (days <= 0) return words.Today;
            if (days == 1) return words.Yesterday;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, words.DaysAgo, days);
        }
    }
}
