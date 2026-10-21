// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: C09 import plan for Valheim_Admin_Tool's admin-function hotkeys (design 10). The user's own config
// file is read as data (section and key names); no code of that mod is used. Same parser model as Tempest.Core (expDARE).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Thing.Config;

namespace ExtraPotions.Thing.Conversion
{
    public readonly struct ImportValue
    {
        public ImportValue(string settingId, string value, string source)
        {
            SettingId = settingId;
            Value = value;
            Source = source;
        }

        public string SettingId { get; }

        /// <summary>BepInEx's serialized keyboard shortcut text, copied as written.</summary>
        public string Value { get; }

        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(IReadOnlyList<ImportValue> values, IReadOnlyList<string> notes)
        {
            Values = values;
            Notes = notes;
        }

        public IReadOnlyList<ImportValue> Values { get; }

        public IReadOnlyList<string> Notes { get; }

        public string Report() => string.Join("\n", new[] { "Source: Valheim_Admin_Tool" }
            .Concat(Values.Select(v => "Set " + v.SettingId + " = " + v.Value + " (from " + v.Source + ")"))
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
        public const string AdminToolFile = "com.icecub.valheimadmintool.cfg";
        const string HotkeySection = "Hotkeys";

        /// <summary>Valheim_Admin_Tool <c>[Hotkeys]</c> keys and the Thing setting each one fills. Every other hotkey is a cheat (Hex).</summary>
        public static IReadOnlyDictionary<string, string> Targets { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "WardOverride", ThingSettings.Hotkeys.WardOverride.Id },
            { "RemoveDrops", ThingSettings.Hotkeys.RemoveDrops.Id },
        };

        public static ImportPlan Plan(string? text)
        {
            var values = new List<ImportValue>();
            var notes = new List<string>();
            var entries = SourceConfig.Parse(text);
            foreach (var e in entries.Where(e => e.Section == HotkeySection))
            {
                if (!Targets.TryGetValue(e.Key, out var target)) continue;
                if (e.Value.Length == 0 || string.Equals(e.Value, "None", StringComparison.OrdinalIgnoreCase)) continue;
                values.Add(new ImportValue(target, e.Value, "[" + HotkeySection + "] " + e.Key));
            }
            if (entries.Any(e => e.Section == "Automatic resets"))
                notes.Add("[Automatic resets]: dungeon and resource resets belong to Delve; its converter imports them");
            var menu = entries.FirstOrDefault(e => e.Section == "General" && e.Key == "Toggle menu");
            if (menu.Key != null)
                notes.Add("[General] Toggle menu = " + menu.Value + ": not imported; that menu also holds the cheats (Hex), Thing keeps its own Admin Window Key");
            return new ImportPlan(values, notes);
        }
    }
}
