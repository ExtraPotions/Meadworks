// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Source config parsing reuses Meadworks' own Ember SourceConfig baseline; mappings follow Magpie design9.2.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using ExtraPotions.Magpie.Config;

namespace ExtraPotions.Magpie.Conversion
{
    public enum ImportSource { SwiftPickup, FloatingItems, LegacyFloatingItems }
    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source, IDictionary<string, object> values, IList<string> notes)
        {
            Source = source;
            Values = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(values, StringComparer.Ordinal));
            Notes = Array.AsReadOnly(notes.ToArray());
        }
        public ImportSource Source { get; }
        public IReadOnlyDictionary<string, object> Values { get; }
        public IReadOnlyList<string> Notes { get; }
        public override string ToString() => string.Join("\n", new[] { "Source: " + Source }
            .Concat(Values.Select(pair => "Set " + pair.Key + " = " + Convert.ToString(pair.Value, CultureInfo.InvariantCulture)))
            .Concat(Notes));
    }
    public static class SourceImport
    {
        // Mirrors Crucible MeadworksMenu.DefaultChord; Magpie.Core has no Crucible reference.
        const string MeadworksMenuKey = "F8";
        public static string FileName(ImportSource source) => source switch
        {
            ImportSource.SwiftPickup => "user.swiftpickup.cfg",
            ImportSource.FloatingItems => "com.ab5olutezer0.valheim.floatingitems.cfg",
            ImportSource.LegacyFloatingItems => "com.michal.valheim.floatingitems.cfg",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        public static string SourceId(ImportSource source) => source switch
        {
            ImportSource.SwiftPickup => "user.swiftpickup",
            ImportSource.FloatingItems => "com.ab5olutezer0.valheim.floatingitems",
            ImportSource.LegacyFloatingItems => "com.michal.valheim.floatingitems",
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        static SettingSpec? Setting(ImportSource source, string key)
        {
            if (source != ImportSource.SwiftPickup)
                return key == "General.ExcludedItems" ? MagpieSettings.ExcludedItems : null;
            return key switch
            {
                "General.Enabled" => MagpieSettings.AutoPickupAtStart,
                "General.PickupRange" => MagpieSettings.PickupRange,
                "General.ScanIntervalSeconds" => MagpieSettings.ScanInterval,
                "General.MaxPickupsPerScan" => MagpieSettings.MaxPickupsPerScan,
                "General.MagnetSpeed" => MagpieSettings.MagnetSpeed,
                "General.IgnoreItems" => MagpieSettings.IgnoreItems,
                "General.ToggleKey" => MagpieSettings.PickupToggleKey,
                "Loot.LootMultiplier" => MagpieSettings.LootMultiplier,
                "Merge.MergeNearbyDrops" => MagpieSettings.MergeEnabled,
                "Merge.MergeRadius" => MagpieSettings.MergeRadius,
                "Merge.MergeMinCount" => MagpieSettings.MergeMinCount,
                "Merge.MaxMergesPerScan" => MagpieSettings.MaxMergesPerScan,
                "Performance.SkipPickupEffects" => MagpieSettings.SkipPickupEffects,
                "Performance.ShowPickupMessages" => MagpieSettings.ShowPickupMessages,
                _ => null
            };
        }
        public static ImportPlan Plan(ImportSource source, string text)
        {
            FileName(source);
            if (text == null || text.Length > 1024 * 1024) throw new ArgumentException("Import source exceeds 1 MiB or is missing.");
            var values = new Dictionary<string, object>(StringComparer.Ordinal);
            var notes = new List<string>();
            string section = "";
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim().TrimStart('\ufeff');
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                if (line[0] == '[' && line[line.Length - 1] == ']') { section = line.Substring(1, line.Length - 2).Trim(); continue; }
                int equals = line.IndexOf('=');
                if (equals <= 0) { notes.Add("Skipped unrecognized source line."); continue; }
                string key = section + "." + line.Substring(0, equals).Trim(), textValue = line.Substring(equals + 1).Trim();
                var setting = Setting(source, key);
                if (setting == null) { notes.Add("Skipped unknown key: " + key); continue; }
                if (values.ContainsKey(setting.Id)) throw new FormatException("Duplicate source setting: " + key);
                object value;
                if (setting.Default is bool)
                {
                    if (!bool.TryParse(textValue, out bool flag)) throw new FormatException("Invalid boolean: " + key);
                    value = flag;
                }
                else if (setting.Default is int)
                {
                    if (!long.TryParse(textValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer)) throw new FormatException("Invalid integer: " + key);
                    int clamped = (int)Math.Max(setting.Minimum!.Value, Math.Min(setting.Maximum!.Value, integer));
                    if (clamped != integer) notes.Add("Clamped " + key + " to " + clamped.ToString(CultureInfo.InvariantCulture));
                    value = clamped;
                }
                else if (setting.Default is float)
                {
                    if (!double.TryParse(textValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number))
                        throw new FormatException("Invalid finite number: " + key);
                    float clamped = (float)Math.Max(setting.Minimum!.Value, Math.Min(setting.Maximum!.Value, number));
                    if (number < setting.Minimum.Value || number > setting.Maximum.Value) notes.Add("Clamped " + key + " to " + clamped.ToString("R", CultureInfo.InvariantCulture));
                    value = clamped;
                }
                else if (setting == MagpieSettings.PickupToggleKey && string.Equals(textValue, MeadworksMenuKey, StringComparison.OrdinalIgnoreCase))
                {
                    value = "";
                    notes.Add("Pickup Toggle Key left unbound: F8 is the Meadworks menu key.");
                }
                else value = textValue;
                values.Add(setting.Id, value);
            }
            return new ImportPlan(source, values, notes);
        }
    }
}
