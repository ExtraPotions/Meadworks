// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 import plan for NoFogBruh's [Weather Events - *] sections (Cairn Q2, design 9). Reading the
// source config's keys is data; WildWeather has no config sample, so there is no WildWeather converter.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Tempest.Config;
using ExtraPotions.Tempest.Weather;

namespace ExtraPotions.Tempest.Conversion
{
    public readonly struct ImportValue
    {
        public ImportValue(string settingId, object value, string source)
        {
            SettingId = settingId;
            Value = value;
            Source = source;
        }

        public string SettingId { get; }
        public object Value { get; }
        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(IReadOnlyList<ImportValue> values, IReadOnlyList<string> otherDisabled, IReadOnlyList<string> notes)
        {
            Values = values;
            OtherDisabled = otherDisabled;
            Notes = notes;
        }

        public IReadOnlyList<ImportValue> Values { get; }

        /// <summary>Switched-off environments NoFogBruh created dynamically; they join <c>Disabled Environments</c>.</summary>
        public IReadOnlyList<string> OtherDisabled { get; }

        public IReadOnlyList<string> Notes { get; }

        public string Report() => string.Join("\n", new[] { "Source: NoFogBruh" }
            .Concat(Values.Select(v => "Set " + v.SettingId + " = " + ((bool)v.Value ? "true" : "false") + " (from " + v.Source + ")"))
            .Concat(OtherDisabled.Count == 0 ? Enumerable.Empty<string>()
                : new[] { "Set " + TempestSettings.Filter.OtherDisabled.Id + " = " + string.Join(", ", OtherDisabled) + " (merged with the current list)" })
            .Concat(Notes.Select(n => "Note: " + n)));
    }

    public readonly struct SourceEntry
    {
        public SourceEntry(string section, string key, string value)
        {
            Section = section;
            Key = key;
            Value = value;
        }

        public string Section { get; }
        public string Key { get; }
        public string Value { get; }
    }

    /// <summary>Reads a BepInEx config file's sections and <c>Key = Value</c> lines; comments and other lines are ignored.</summary>
    public static class SourceConfig
    {
        public static IReadOnlyList<SourceEntry> Parse(string? text)
        {
            var result = new List<SourceEntry>();
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
                if (key.Length == 0) continue;
                result.Add(new SourceEntry(section, key, line.Substring(eq + 1).Trim()));
            }
            return result;
        }
    }

    public static class SourceImport
    {
        public const string NoFogBruhFile = "vapok.mods.nofogbruh.cfg";
        const string SectionPrefix = "Weather Events - ";

        public static ImportPlan Plan(string? text)
        {
            var values = new List<ImportValue>();
            var other = new List<string>();
            var notes = new List<string>();
            var bySource = WeatherSwitches.All.ToDictionary(s => s.SourceSection + "\n" + s.Key, StringComparer.Ordinal);
            foreach (var e in SourceConfig.Parse(text))
            {
                if (!e.Section.StartsWith(SectionPrefix, StringComparison.Ordinal)) continue;
                var where = "[" + e.Section + "] " + e.Key;
                if (!bool.TryParse(e.Value, out var on))
                {
                    notes.Add(where + " = " + e.Value + ": not true or false, skipped");
                    continue;
                }
                if (bySource.TryGetValue(e.Section + "\n" + e.Key, out var s))
                {
                    values.Add(new ImportValue(TempestSettings.Filter.Switches[s].Id, on, where));
                    continue;
                }
                var env = DynamicEnvironment(e.Key);
                if (env == null)
                {
                    notes.Add(where + ": unknown key, skipped");
                    continue;
                }
                if (on) notes.Add(where + ": other environment " + env + " stays allowed (nothing to import)");
                else other.Add(env);
            }
            return new ImportPlan(values, other.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), notes);
        }

        /// <summary>The current list with the imported names added once (first spelling wins), canonical text.</summary>
        public static string MergeOther(string? current, IEnumerable<string> imported) =>
            WeatherFilter.NormalizeList(string.Join(",", new[] { current ?? "" }.Concat(imported ?? Enumerable.Empty<string>())));

        /// <summary>NoFogBruh names dynamic switches <c>Enable &lt;environment&gt; Weather</c>.</summary>
        static string? DynamicEnvironment(string key)
        {
            const string Head = "Enable ";
            const string Tail = " Weather";
            if (!key.StartsWith(Head, StringComparison.Ordinal) || !key.EndsWith(Tail, StringComparison.Ordinal) || key.Length <= Head.Length + Tail.Length) return null;
            var env = key.Substring(Head.Length, key.Length - Head.Length - Tail.Length).Trim();
            return env.Length == 0 || env.IndexOf(' ') >= 0 ? null : env;
        }
    }
}
