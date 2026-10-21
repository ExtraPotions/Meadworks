// Bestiary - ExtraPotions Meadworks creatures and bone arsenal.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 import plan for EliteCreaturesPack's config (design 11.2). Only section and key names of the
// source file are used; Bestiary keeps them, so the mapping is one-to-one except for keys Bestiary spells the American way.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Bestiary.Config;

namespace ExtraPotions.Bestiary.Conversion
{
    public enum ImportNoteKind
    {
        /// <summary>Section or key Bestiary does not import (the lock, a newer ECP's settings, unknown).</summary>
        Skipped,

        /// <summary>A number outside Bestiary's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value; Bestiary's current value is kept.</summary>
        Fallback,
    }

    public sealed class ImportNote
    {
        internal ImportNote(ImportNoteKind kind, string source, string text)
        {
            Kind = kind;
            Source = source;
            Text = text;
        }

        public ImportNoteKind Kind { get; }

        /// <summary><c>Section.Key</c> in the source file.</summary>
        public string Source { get; }

        public string Text { get; }

        public override string ToString() => Kind + " " + Source + ": " + Text;
    }

    public sealed class ImportValue
    {
        internal ImportValue(string settingId, object value, string source)
        {
            SettingId = settingId;
            Value = value;
            Source = source;
        }

        public string SettingId { get; }

        /// <summary>The value typed as the Bestiary setting (bool, int, float, string or <see cref="BiomeMask"/>).</summary>
        public object Value { get; }

        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(IReadOnlyList<ImportValue> values, IReadOnlyList<ImportNote> notes)
        {
            Values = values;
            Notes = notes;
        }

        public IReadOnlyList<ImportValue> Values { get; }
        public IReadOnlyList<ImportNote> Notes { get; }

        /// <summary>Plain text preview report, one line per value and note.</summary>
        public string Report() =>
            string.Join("\n", new[] { "Source: " + EcpImport.FileName }
                .Concat(Values.Select(v => "Set " + v.SettingId + " = " + Format(v.Value) + " (from " + v.Source + ")"))
                .Concat(Notes.Select(n => n.ToString())));

        internal static string Format(object value) => value is float f ? f.ToString("R", CultureInfo.InvariantCulture)
            : value is double d ? d.ToString("R", CultureInfo.InvariantCulture)
            : value is bool b ? (b ? "true" : "false")
            : value is BiomeMask m ? BiomeMasks.Format(m)
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    /// <summary>Reads a BepInEx config file's sections and <c>Key = Value</c> lines; comments and other lines are ignored.</summary>
    public static class SourceConfig
    {
        public static IReadOnlyList<(string Section, string Key, string Value)> Parse(string? text)
        {
            var result = new List<(string, string, string)>();
            var section = "";
            foreach (var raw in (text ?? "").Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                var eq = line.IndexOf('=');
                if (eq <= 0) continue;
                var key = line.Substring(0, eq).Trim();
                if (key.Length > 0) result.Add((section, key, line.Substring(eq + 1).Trim()));
            }
            return result;
        }
    }

    /// <summary>Plans the import of <c>com.EliteCreaturesPack.cfg</c>: every key of the same name, typed and clamped.</summary>
    public static class EcpImport
    {
        public const string FileName = "com.EliteCreaturesPack.cfg";

        private const string Lock = "1 - General.Lock Configuration";

        /// <summary>ECP keys whose Bestiary key is spelled the American way.</summary>
        private static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>
        {
            { "4 - Rime Giant.Armoured Damage", "4 - Rime Giant.Armored Damage" },
        };

        public static ImportPlan Plan(string? text)
        {
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            foreach (var (section, key, raw) in SourceConfig.Parse(text))
            {
                var id = section + "." + key;
                if (id == Lock)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, id, "dropped; Crucible binds server-authoritative settings to the server"));
                    continue;
                }
                var target = Renamed.TryGetValue(id, out var renamed) ? renamed : id;
                var spec = BestiarySettings.Find(target);
                if (spec == null)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, id, "no Bestiary setting"));
                    continue;
                }
                var value = Read(spec, raw, id, notes);
                if (value != null) values.Add(new ImportValue(target, value, id));
            }
            return new ImportPlan(values, notes);
        }

        private static object? Read(SettingSpec spec, string raw, string id, List<ImportNote> notes)
        {
            switch (spec.Default)
            {
                case bool _:
                    if (bool.TryParse(raw, out var flag)) return flag;
                    return Fallback(notes, id, raw, "is not true or false");
                case int _:
                    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return Fallback(notes, id, raw, "is not a number");
                    return Clamp(spec, whole, spec.Clamp(whole), raw, id, notes);
                case float _:
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || float.IsNaN(number) || float.IsInfinity(number))
                        return Fallback(notes, id, raw, "is not a number");
                    return Clamp(spec, number, spec.Clamp(number), raw, id, notes);
                case BiomeMask _:
                    var mask = BiomeMasks.Parse(raw);
                    return mask.HasValue ? (object)mask.Value : Fallback(notes, id, raw, "is not a biome list");
                default:
                    return raw;
            }
        }

        private static object Clamp<T>(SettingSpec spec, T value, T clamped, string raw, string id, List<ImportNote> notes)
        {
            if (!EqualityComparer<T>.Default.Equals(value, clamped))
                notes.Add(new ImportNote(ImportNoteKind.Clamped, id,
                    raw + " is outside " + ImportPlan.Format(spec.Minimum!.Value) + " to " + ImportPlan.Format(spec.Maximum!.Value) + "; " + ImportPlan.Format(clamped!) + " used"));
            return clamped!;
        }

        private static object? Fallback(List<ImportNote> notes, string id, string raw, string why)
        {
            notes.Add(new ImportNote(ImportNoteKind.Fallback, id, "\"" + raw + "\" " + why + "; Bestiary's value kept"));
            return null;
        }
    }
}
