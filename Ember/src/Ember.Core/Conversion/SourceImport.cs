// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings from the three source configs (design 10.3). Only section and key names of
// the source files are used; no source code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Ember.Config;
using ExtraPotions.Ember.Doors;

namespace ExtraPotions.Ember.Conversion
{
    public enum ImportSource { BetterSleepBruh, TorchesAreFires, DoorOpenerBruh }

    public enum ImportNoteKind
    {
        /// <summary>Section or key that Ember does not import (telemetry, logging, server sync lock, unknown).</summary>
        Skipped,

        /// <summary>A number outside Ember's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value or an unknown enum name; Ember's default is used.</summary>
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

        /// <summary>The normalized value, typed as the Ember setting (bool, int, float or AutomationMechanic).</summary>
        public object Value { get; }

        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source, IReadOnlyList<ImportValue> values, IReadOnlyList<ImportNote> notes)
        {
            Source = source;
            Values = Array.AsReadOnly(values.ToArray());
            Notes = Array.AsReadOnly(notes.ToArray());
        }

        public ImportSource Source { get; }
        public IReadOnlyList<ImportValue> Values { get; }
        public IReadOnlyList<ImportNote> Notes { get; }

        /// <summary>Plain text Preview report, one line per value and note.</summary>
        public string Report() =>
            string.Join("\n", new[] { "Source: " + Source }
                .Concat(Values.Select(v => "Set " + v.SettingId + " = " + Format(v.Value) + " (from " + v.Source + ")"))
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

    /// <summary>Plans an import: source entries to Ember setting values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.BetterSleepBruh: return "vapok.mods.BetterSleepBruh.cfg";
                case ImportSource.TorchesAreFires: return "blacks7ar.TorchesAreFires.cfg";
                case ImportSource.DoorOpenerBruh: return "vapok.mods.dooropenerbruh.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        static readonly Dictionary<string, EmberSetting> Bsb = new Dictionary<string, EmberSetting>(StringComparer.Ordinal)
        {
            { "Server Settings.Use Vanilla Sleep Start", EmberSettings.Sleep.UseVanillaSleepWindow },
            { "Server Settings.Sleep Start", EmberSettings.Sleep.SleepStart },
            { "Server Settings.Bonus Multiplier", EmberSettings.Sleep.BonusMultiplier },
            { "Server Settings.Bonus Increment Scale", EmberSettings.Sleep.BonusIncrementScale },
            { "Server Settings.Boost Fade (Real Seconds)", EmberSettings.Sleep.BoostFadeSeconds },
            { "Testing Mode.Enable Testing Mode", EmberSettings.Sleep.TestingMode },
            { "Testing Mode.Fake Total Players", EmberSettings.Sleep.FakeTotalPlayers },
            { "Testing Mode.Simulate Players In Bed", EmberSettings.Sleep.SimulatePlayersInBed },
            // BetterSleepBruh 2.1.0: the station safeguards Ember adopted keep their section and key names.
            { "Station Timers.Protect Cooking Timers", EmberSettings.Stations.ProtectCookingTimers },
            { "Station Timers.Protect Smelter Timers", EmberSettings.Stations.ProtectSmelterTimers },
            { "Station Timers.Protect Crop Timers", EmberSettings.Stations.ProtectCropTimers },
            { "Station Timers.Protect Mead Timers", EmberSettings.Stations.ProtectMeadTimers },
            { "Station Timers.Protect Pickable Timers", EmberSettings.Stations.ProtectPickableTimers },
            { "Ocean & Boat Physics.Decouple Water Wave Speed", EmberSettings.Ocean.DecoupleWaterWaveSpeed },
            { "Ocean & Boat Physics.Calm Ocean During Sleep", EmberSettings.Ocean.CalmOceanDuringSleep },
            { "Ocean & Boat Physics.Ocean Calm Multiplier", EmberSettings.Ocean.OceanCalmMultiplier },
            { "Ocean & Boat Physics.Prevent Boat Sleep Impact Damage", EmberSettings.Ocean.PreventBoatSleepImpactDamage },
            { "Ocean & Boat Physics.Suppress Boat Impact Screen Shake", EmberSettings.Ocean.SuppressBoatImpactScreenShake },
        };

        static readonly Dictionary<string, EmberSetting> Taf = new Dictionary<string, EmberSetting>(StringComparer.Ordinal)
        {
            { "2- General.Environment Affects Torch", EmberSettings.Torches.EnvironmentAffectsTorch },
            { "2- General.Cold Biome Affects Torch", EmberSettings.Torches.ColdBiomeAffectsTorch },
            { "2- General.Burn Rate", EmberSettings.Torches.BurnRate },
        };

        public static ImportPlan Plan(ImportSource source, string? text)
        {
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                var target = Map(source, entry);
                if (target == null)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, SkipReason(source, entry)));
                    continue;
                }
                if (values.Any(v => v.SettingId == target.Id)) continue;
                values.Add(new ImportValue(target.Id, Convert(target, entry.Value, from, notes), from));
            }
            return new ImportPlan(source, values, notes);
        }

        static string SkipReason(ImportSource source, SourceEntry entry) => "not imported";

        static EmberSetting? Map(ImportSource source, SourceEntry entry)
        {
            var from = entry.Section + "." + entry.Key;
            switch (source)
            {
                case ImportSource.BetterSleepBruh:
                    return Bsb.TryGetValue(from, out var s) ? s : null;
                case ImportSource.TorchesAreFires:
                    return Taf.TryGetValue(from, out var t) ? t : null;
                case ImportSource.DoorOpenerBruh:
                    if (from == "Local Settings.Enable Auto Door") return EmberSettings.Doors.EnableAutoDoor;
                    var kind = DoorKinds.All.FirstOrDefault(k => k.Section == entry.Section);
                    if (kind == null) return null;
                    var door = EmberSettings.Door(kind);
                    switch (entry.Key)
                    {
                        case "Automation Mechanic": return door.Mechanic;
                        case "Open Distance": return door.OpenDistance;
                        case "Close Distance": return door.CloseDistance;
                        case "Check for Key": return door.CheckForKey;
                        default: return null;
                    }
                default:
                    return null;
            }
        }

        static object Convert(EmberSetting target, string text, string from, List<ImportNote> notes)
        {
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Ember default " + ImportPlan.Format(target.DefaultValue)));
                return target.DefaultValue;
            }
            object Checked(object value)
            {
                var normalized = target.NormalizeBoxed(value);
                if (!Equals(normalized, value))
                    notes.Add(new ImportNote(ImportNoteKind.Clamped, from, ImportPlan.Format(value) + " clamped to " + ImportPlan.Format(normalized)));
                return normalized;
            }
            switch (target)
            {
                case EmberSetting<bool> _:
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "On", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || string.Equals(text, "Off", StringComparison.OrdinalIgnoreCase)) return false;
                    return Fallback("unreadable \"" + text + "\"");
                case EmberSetting<float> _:
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                        ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case EmberSetting<int> _:
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? Checked(i) : Fallback("unreadable \"" + text + "\"");
                case EmberSetting<AutomationMechanic> _:
                    return DoorRules.TryParseMechanic(text, out var mechanic) ? (object)mechanic : Fallback("unknown mechanic \"" + text + "\"");
                default:
                    return Fallback("unsupported type");
            }
        }
    }
}
