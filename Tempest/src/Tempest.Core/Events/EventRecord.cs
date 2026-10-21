// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the shared event record (design 1.2, 3, W5, W11) as world global key text.
using System;
using System.Globalization;

namespace ExtraPotions.Tempest.Events
{
    /// <summary>
    /// Text form of <see cref="ScheduleState"/> for the world key <see cref="TempestCore.WorldKey"/>. The game lowercases
    /// global keys and splits key and value at the first space, so the text uses lowercase letters, digits and
    /// <c>. + - ; ,</c> only:
    /// <c>t1;&lt;next serial&gt;;&lt;last rolled day&gt;;&lt;last start day&gt;;&lt;event&gt;</c> where the event is <c>-</c> or
    /// <c>&lt;serial&gt;,&lt;b|t&gt;,&lt;start&gt;,&lt;warning&gt;,&lt;active&gt;,&lt;clearing&gt;,&lt;seed&gt;,&lt;biome flags&gt;</c>.
    /// The record never carries the dry flag (older clients must keep reading it); see <see cref="DryMark"/>. A Lightning
    /// Storm is written as a thunderstorm (<c>t</c>), and a kind older clients do not know (<see cref="EventKinds.Extended"/>)
    /// is written as no event (<c>-</c>) with its serial counted, the event itself going to <see cref="ExtendedRecord"/>.
    /// Anything malformed, out of range or longer than <see cref="MaximumLength"/> is refused (the reader keeps idle weather).
    /// </summary>
    public static class EventRecord
    {
        public const string Version = "t1";
        public const int MaximumLength = 256;

        public static string Encode(ScheduleState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var e = state.Current;
            var evt = e == null || EventKinds.Extended(e.Kind) ? "-" : Fields(e, e.Kind == EventKind.Blizzard ? "b" : "t");
            return string.Join(";", Version, I(state.NextSerial), state.LastRolledDay.ToString(CultureInfo.InvariantCulture),
                state.LastStartDay.ToString(CultureInfo.InvariantCulture), evt).ToLowerInvariant();
        }

        public static bool TryDecode(string? text, out ScheduleState state)
        {
            state = ScheduleState.Empty;
            if (string.IsNullOrEmpty(text) || text!.Length > MaximumLength) return false;
            foreach (var c in text)
                if (!(c >= '0' && c <= '9') && c != '.' && c != '+' && c != '-' && c != ';' && c != ',' && c != 'b' && c != 't' && c != 'e') return false;
            var parts = text.Split(';');
            if (parts.Length != 5 || parts[0] != Version) return false;
            if (!int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var next)) return false;
            if (!long.TryParse(parts[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var rolled)) return false;
            if (!long.TryParse(parts[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var started)) return false;
            TempestEvent? current = null;
            if (parts[4] != "-" && !TryEvent(parts[4], out current)) return false;
            try
            {
                state = new ScheduleState(current, rolled, started, next);
                return true;
            }
            catch (ArgumentException)
            {
                state = ScheduleState.Empty;
                return false;
            }
        }

        /// <summary>The canonical text of a valid record, or null (the canonicalizer of the Crucible world setting binding).</summary>
        public static string? Canonical(string? text) => TryDecode(text, out var state) ? Encode(state) : null;

        static bool TryEvent(string text, out TempestEvent? e) =>
            TryFields(text, letter => letter == "b" ? EventKind.Blizzard : letter == "t" ? EventKind.Thunderstorm : (EventKind?)null, out e);

        /// <summary>The comma-separated event fields with <paramref name="letter"/> as the kind.</summary>
        internal static string Fields(TempestEvent e, string letter) => string.Join(",", e.Serial.ToString(CultureInfo.InvariantCulture), letter,
            e.Start.ToString("R", CultureInfo.InvariantCulture), I(e.WarningSeconds), I(e.ActiveSeconds), I(e.ClearingSeconds), I(e.Seed), I((int)e.Biomes));

        /// <summary>Parses the comma-separated event fields; <paramref name="kindOf"/> maps the kind letter (null: refused).</summary>
        internal static bool TryFields(string text, Func<string, EventKind?> kindOf, out TempestEvent? e)
        {
            e = null;
            var f = text.Split(',');
            if (f.Length != 8) return false;
            var kind = kindOf(f[1]);
            if (kind == null) return false;
            if (!Int(f[0], out var serial) || !Int(f[3], out var warning) || !Int(f[4], out var active) || !Int(f[5], out var clearing)
                || !Int(f[6], out var seed) || !Int(f[7], out var biomes)) return false;
            if (!double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var start) || double.IsNaN(start) || double.IsInfinity(start)) return false;
            try
            {
                e = new TempestEvent(serial, kind.Value, start, warning, active, clearing, seed, (WeatherBiome)biomes);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        static bool Int(string s, out int value) => int.TryParse(s, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        static string I(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The dry flag, kept in its own world key next to the record so clients without dry storms read the record unchanged
    /// and see a normal thunderstorm. The text is the serial of the dry event, or <c>0</c>. A world without the key, or a
    /// mark naming another event, reads as not dry.
    /// </summary>
    public static class DryMark
    {
        public const string None = "0";

        public static string Encode(TempestEvent? e) => e != null && e.Dry ? e.Serial.ToString(CultureInfo.InvariantCulture) : None;

        /// <summary>The canonical text of a valid mark (digits, no sign, no leading zero), or null.</summary>
        public static string? Canonical(string? text)
        {
            if (string.IsNullOrEmpty(text) || text!.Length > 10) return null;
            foreach (var c in text) if (c < '0' || c > '9') return null;
            if (text.Length > 1 && text[0] == '0') return null;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? text : null;
        }

        /// <summary>The state with its current event marked dry when <paramref name="mark"/> names that thunderstorm.</summary>
        public static ScheduleState Apply(ScheduleState state, string? mark)
        {
            var e = state.Current;
            if (e == null || e.Kind != EventKind.Thunderstorm || Canonical(mark) != e.Serial.ToString(CultureInfo.InvariantCulture)) return state;
            if (e.Dry) return state;
            return state.With(new TempestEvent(e.Serial, e.Kind, e.Start, e.WarningSeconds, e.ActiveSeconds, e.ClearingSeconds, e.Seed, e.Biomes, true));
        }
    }

    /// <summary>
    /// The event of a kind older clients do not know (<see cref="EventKinds.Extended"/>), kept in its own guarded world key
    /// <see cref="TempestCore.ExtendedWorldKey"/> next to the record. The main record then holds no event (older clients see
    /// nothing and keep their own weather) but counts the serial. Text: <c>0</c> for none, else
    /// <c>x1;&lt;serial&gt;,&lt;f|a|m&gt;,&lt;start&gt;,&lt;warning&gt;,&lt;active&gt;,&lt;clearing&gt;,&lt;seed&gt;,&lt;biome flags&gt;</c>
    /// (f Dense Fog, a Ash Storm, m Meteor Shower). The event applies only while the record holds no event and the serial
    /// is the record's last one, so a stale key never brings an old event back.
    /// </summary>
    public static class ExtendedRecord
    {
        public const string None = "0";
        public const string Version = "x1";

        public static string Encode(TempestEvent? e) =>
            e == null || !EventKinds.Extended(e.Kind) ? None : Version + ";" + EventRecord.Fields(e, Letter(e.Kind)).ToLowerInvariant();

        public static string? Canonical(string? text)
        {
            if (text == None) return None;
            return TryDecode(text, out var e) ? Encode(e) : null;
        }

        public static bool TryDecode(string? text, out TempestEvent? e)
        {
            e = null;
            if (string.IsNullOrEmpty(text) || text!.Length > EventRecord.MaximumLength || !text.StartsWith(Version + ";", StringComparison.Ordinal)) return false;
            return EventRecord.TryFields(text.Substring(Version.Length + 1), Kind, out e);
        }

        /// <summary>The state with the extended event as its current event when the record holds none and the serial matches.</summary>
        public static ScheduleState Apply(ScheduleState state, string? text)
        {
            if (state.Current != null || !TryDecode(text, out var e) || e!.Serial != state.NextSerial - 1) return state;
            return state.With(e);
        }

        static string Letter(EventKind kind) => kind == EventKind.DenseFog ? "f" : kind == EventKind.AshStorm ? "a" : "m";

        static EventKind? Kind(string letter) => letter == "f" ? EventKind.DenseFog : letter == "a" ? EventKind.AshStorm : letter == "m" ? EventKind.MeteorShower : (EventKind?)null;
    }

    /// <summary>Which record text to use: the guarded world key, else a readable session 1 <c>tempest.event</c> value.</summary>
    public static class RecordMigration
    {
        public readonly struct Choice
        {
            public Choice(string? text, bool migrate)
            {
                Text = text;
                Migrate = migrate;
            }

            /// <summary>The canonical record to use, or null when neither key holds a readable record.</summary>
            public string? Text { get; }

            /// <summary>The authority copies <see cref="Text"/> to the guarded key; the old key is never changed.</summary>
            public bool Migrate { get; }
        }

        public static Choice Choose(string? current, string? legacy)
        {
            var c = EventRecord.Canonical(current);
            if (c != null) return new Choice(c, false);
            var l = EventRecord.Canonical(legacy);
            return l != null ? new Choice(l, true) : new Choice(null, false);
        }
    }
}
