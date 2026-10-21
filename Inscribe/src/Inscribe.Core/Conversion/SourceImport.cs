// Inscribe - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings (design 10.3), same plan model as Wildblood.Core and Ember.Core (expDARE). Only
// section, key and value names of the source files are used; no source code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Inscribe.Config;

namespace ExtraPotions.Inscribe.Conversion
{
    /// <summary>Import sources; the ItemEnhancements imports were removed with enhancement (0.2.0).</summary>
    public enum ImportSource { EliteCraftingConfig }

    public enum ImportNoteKind
    {
        /// <summary>Deliberately not imported (server lock, a rule-file block, a cheat from a remote file).</summary>
        Skipped,

        /// <summary>A key Inscribe does not know.</summary>
        Unknown,

        /// <summary>A number outside Inscribe's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value or an unknown name; Inscribe's default is used.</summary>
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

        /// <summary>The normalized value, typed as the Inscribe setting.</summary>
        public object Value { get; }

        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source, IReadOnlyList<ImportValue> values, IReadOnlyList<ImportNote> notes, string? logOverride)
        {
            Source = source;
            Values = values;
            Notes = notes;
            LogOverride = logOverride;
        }

        public ImportSource Source { get; }
        public IReadOnlyList<ImportValue> Values { get; }
        public IReadOnlyList<ImportNote> Notes { get; }

        /// <summary>A Crucible log level override (C06) from EliteCrafting's diagnostics, e.g. <c>inscribe=Verbose</c>; null for none.</summary>
        public string? LogOverride { get; }

        /// <summary>Plain text Preview report, one line per value and note.</summary>
        public string Report() =>
            string.Join("\n", new[] { "Source: " + Source }
                .Concat(Values.Select(v => "Set " + v.SettingId + " = " + Format(v.Value) + " (from " + v.Source + ")"))
                .Concat(LogOverride == null ? Array.Empty<string>() : new[] { "Log override " + LogOverride })
                .Concat(Notes.Select(n => n.ToString())));

        internal static string Format(object value) => value is float f ? f.ToString("R", CultureInfo.InvariantCulture)
            : value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
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

    /// <summary>One YAML family file to copy (design 10.3): source name, Inscribe name.</summary>
    public readonly struct YamlCopy
    {
        public YamlCopy(string from, string to)
        {
            From = from;
            To = to;
        }

        public string From { get; }
        public string To { get; }
    }

    /// <summary>Plans an import (C09 Preview): source entries to Inscribe setting values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.EliteCraftingConfig: return "com.EliteCrafting.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        private const string EcGeneral = "1 - General";
        private const string EcRunes = "2 - Runes";
        private const string EcDrops = "3 - Drops";
        private const string EcCommands = "4 - Commands";
        private const string EcDisplay = "5 - Display (per player)";
        private const string EcGlow = "6 - Ground glow (per player)";
        private const string EcDiagnostics = "7 - Diagnostics";
        private const string EcEcr = "8 - Elite Creatures Reborn";

        private static readonly Dictionary<string, InscribeSetting> Ec = new Dictionary<string, InscribeSetting>(StringComparer.Ordinal)
        {
            { EcGeneral + ".Inscription effects", InscribeSettings.General.InscriptionEffects },
            { EcRunes + ".Modify equipped items", InscribeSettings.Runes.ModifyEquippedItems },
            { EcRunes + ".Confirm destructive runes", InscribeSettings.Runes.ConfirmDestructiveRunes },
            { EcDrops + ".Rune drops", InscribeSettings.Drops.RuneDrops },
            { EcDrops + ".Magic item drops", InscribeSettings.Drops.MagicItemDrops },
            { EcCommands + ".Read-only commands for everyone", InscribeSettings.Commands.ReadOnlyForEveryone },
            { EcDisplay + ".Colored item names", InscribeSettings.Display.ColoredItemNames },
            { EcDisplay + ".Tooltip detail", InscribeSettings.Display.TooltipDetail },
            { EcDisplay + ".Show dormant inscriptions", InscribeSettings.Display.ShowDormant },
            { EcGlow + ".Ground glow", InscribeSettings.Glow.GroundGlow },
            { EcGlow + ".Glow intensity", InscribeSettings.Glow.Intensity },
            { EcGlow + ".Glow range", InscribeSettings.Glow.Range },
            { EcGlow + ".Glow max lights", InscribeSettings.Glow.MaxLights },
            { EcGlow + ".Glow refresh seconds", InscribeSettings.Glow.RefreshSeconds },
            { EcGlow + ".Glow runes", InscribeSettings.Glow.GlowRunes },
            { EcGlow + ".Loot beam", InscribeSettings.Glow.LootBeam },
            { EcEcr + ".Synergy", InscribeSettings.Drops.WildbloodStarSynergy },
        };

        private static readonly Dictionary<string, string> EcSkips = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { EcGeneral + ".Lock Configuration", "Crucible decides who may change settings (C01)" },
            // EliteCrafting 0.8.0 and 0.9.1 additions; Inscribe's gameplay stays at 0.7.0 (decision Q1).
            { EcRunes + ".Runes from salvage", "EliteCrafting 0.8+ salvage runes are outside Inscribe's 0.7.0 gameplay" },
            { EcRunes + ".Rune Table", "EliteCrafting 0.8+ Rune Table is outside Inscribe's 0.7.0 gameplay" },
            { EcRunes + ".Gems and sockets", "EliteCrafting 0.8+ gems and sockets are outside Inscribe's 0.7.0 gameplay" },
        };

        private static readonly Dictionary<string, int> BiomeTiers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Meadows", 1 }, { "BlackForest", 2 }, { "Swamp", 3 }, { "Mountain", 4 }, { "Plains", 5 }, { "Mistlands", 6 }, { "AshLands", 7 }, { "DeepNorth", 8 },
            { "Ocean", 4 },
        };

        /// <summary>A Drops.yml biome row's Inscribe tier (design 10.3; the ocean as tier 4 by the death-biome rule); null when unknown.</summary>
        public static int? BiomeTier(string biome) => biome != null && BiomeTiers.TryGetValue(biome, out var tier) ? tier : (int?)null;

        /// <summary>A BepInEx config import: <see cref="ImportSource.EliteCraftingConfig"/>.</summary>
        public static ImportPlan Plan(ImportSource source, string? text)
        {
            if (source != ImportSource.EliteCraftingConfig) throw new ArgumentException("Unknown source.", nameof(source));
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            var maps = Ec;
            bool logRolls = false, logRebuilds = false;
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                if (source == ImportSource.EliteCraftingConfig && entry.Section == EcDiagnostics)
                {
                    if (entry.Key == "Log rolls") logRolls = Eq(entry.Value, "true");
                    else if (entry.Key == "Log effect rebuilds") logRebuilds = Eq(entry.Value, "true");
                    else notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not an Inscribe setting"));
                    continue;
                }
                if (source == ImportSource.EliteCraftingConfig && EcSkips.TryGetValue(from, out var why))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, why));
                    continue;
                }
                if (!maps.TryGetValue(from, out var target))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not an Inscribe setting"));
                    continue;
                }
                Add(target, entry.Value, from, values, notes);
            }
            var log = logRolls ? "inscribe=Verbose" : logRebuilds ? "inscribe=Debug" : null;
            return new ImportPlan(source, values, notes, log);
        }

        /// <summary>
        /// The YAML family files to copy (design 10.3): <c>EliteCrafting_economy*.yml</c> to <c>Inscribe.economy*.yml</c> and
        /// <c>EliteCrafting_inscriptions*.yml</c> to <c>Inscribe.inscriptions*.yml</c>; <c>.v1.bak</c> and other files are ignored.
        /// </summary>
        public static IReadOnlyList<YamlCopy> YamlCopies(IEnumerable<string> fileNames)
        {
            var result = new List<YamlCopy>();
            foreach (var name in fileNames.OrderBy(n => n, StringComparer.Ordinal))
            {
                if (!name.EndsWith(".yml", StringComparison.Ordinal)) continue;
                foreach (var family in new[] { "economy", "inscriptions" })
                {
                    var prefix = "EliteCrafting_" + family;
                    if (name.StartsWith(prefix, StringComparison.Ordinal))
                        result.Add(new YamlCopy(name, "Inscribe." + family + name.Substring(prefix.Length)));
                }
            }
            return result;
        }

        /// <summary>The header line a copied YAML file starts with.</summary>
        public static string CopyHeader(string sourceName) => "# Imported by Meadworks from " + sourceName + " (EliteCrafting 0.7.0 format 2); the source file is unchanged.";

        private static void Add(InscribeSetting target, string text, string from, List<ImportValue> values, List<ImportNote> notes)
        {
            if (values.Any(v => v.SettingId == target.Id))
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "merged into " + target.Id + " (first value kept)"));
                return;
            }
            values.Add(new ImportValue(target.Id, Convert(target, text, from, notes), from));
        }

        private static object Convert(InscribeSetting target, string text, string from, List<ImportNote> notes)
        {
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Inscribe default " + ImportPlan.Format(target.DefaultValue)));
                return target.DefaultValue;
            }
            object Checked(object value)
            {
                var normalized = target.NormalizeBoxed(value);
                if (!Equals(normalized, value))
                    notes.Add(new ImportNote(ImportNoteKind.Clamped, from, ImportPlan.Format(value) + " changed to " + ImportPlan.Format(normalized)));
                return normalized;
            }
            switch (target)
            {
                case InscribeSetting<bool> _:
                    if (Eq(text, "true")) return true;
                    if (Eq(text, "false")) return false;
                    return Fallback("unreadable \"" + text + "\"");
                case InscribeSetting<float> _:
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                        ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case InscribeSetting<int> _:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return Checked(i);
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var whole) && whole == Math.Floor(whole) && Math.Abs(whole) < int.MaxValue
                        ? Checked((int)whole) : Fallback("unreadable \"" + text + "\"");
                case InscribeSetting<string> _:
                    return Checked(text);
                default:
                    if (target.ValueType.IsEnum && target.Choices.Any(c => Eq(c, text.Replace(" ", ""))))
                        return Enum.Parse(target.ValueType, text.Replace(" ", ""), true);
                    return Fallback("unknown value \"" + text + "\"");
            }
        }

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
    }
}
