// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: the PortalSelector converter plan (design 9.2). PortalSelector's config section, key and value
// names are read as data only (behaviour reference, no license); the source file is never edited.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Threshold.Config;
using ExtraPotions.Threshold.Portals;

namespace ExtraPotions.Threshold.Conversion
{
    /// <summary>One <c>Key = Value</c> line of a BepInEx config file under its <c>[Section]</c>.</summary>
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

    public static class SourceConfig
    {
        /// <summary>Entries in file order. Comments (<c>#</c>) and lines without <c>=</c> are skipped; the value is everything after the first <c>=</c>, trimmed.</summary>
        public static IReadOnlyList<SourceEntry> Parse(string? text)
        {
            var entries = new List<SourceEntry>();
            if (string.IsNullOrEmpty(text)) return entries;
            var section = "";
            foreach (var raw in text!.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                entries.Add(new SourceEntry(section, line.Substring(0, eq).Trim(), line.Substring(eq + 1).Trim()));
            }
            return entries;
        }
    }

    public enum ImportNote
    {
        /// <summary><c>[UI] Language</c>: language follows Crucible localization (C05).</summary>
        LanguageNotImported,

        /// <summary><c>[UI] UseGameFont</c>: fonts come from the kit.</summary>
        FontNotImported,

        /// <summary>Where PortalSelector keeps its main portal is not documented; the player sets the hub again with one click.</summary>
        HubNotFound,

        /// <summary>The cheat was imported as on; it applies in single player and on the host, or where the server allows it.</summary>
        CheatAppliesWhereAllowed,

        /// <summary>A value outside the setting's choices was skipped.</summary>
        InvalidValue,
    }

    public sealed class ImportItem
    {
        internal ImportItem(ThresholdSetting setting, string value, string source)
        {
            Setting = setting;
            Value = value;
            Source = source;
        }

        public ThresholdSetting Setting { get; }

        /// <summary>The value in BepInEx text form (bools lower case, choices by name).</summary>
        public string Value { get; }

        /// <summary><c>Section.Key</c> in the source file.</summary>
        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(IReadOnlyList<ImportItem> items, IReadOnlyList<ImportNote> notes)
        {
            Items = items;
            Notes = notes;
        }

        public IReadOnlyList<ImportItem> Items { get; }
        public IReadOnlyList<ImportNote> Notes { get; }
    }

    /// <summary>The hidden <c>Internal.Imported From</c> value: the sources already imported, semicolon separated, in order.</summary>
    public static class ImportMarker
    {
        public static IReadOnlyList<string> Parse(string? value) =>
            (value ?? "").Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).Distinct(StringComparer.Ordinal).ToList();

        public static string Add(string? value, string source) => string.Join(";", Parse(value).Concat(new[] { source.Trim() }).Distinct(StringComparer.Ordinal));

        public static bool Contains(string? value, string source) => Parse(value).Contains(source.Trim(), StringComparer.Ordinal);
    }

    public static class PortalSelectorImport
    {
        public const string FileName = "lazly.valheim.portalselector.cfg";

        /// <summary>PortalSelector's BepInEx GUID (the step-aside table names the same).</summary>
        public const string SourceGuid = "lazly.valheim.portalselector";

        /// <summary>The name written into the import marker and shown in the import notice.</summary>
        public const string SourceName = "PortalSelector";

        /// <summary>Threshold's C09 conversion registration id (T07b).</summary>
        public const string RegistrationId = "threshold." + SourceName;

        /// <summary>What Apply writes through the C09 config target: each planned setting by id (<c>Section.Key</c>) in
        /// plan order, then the import marker with this source added. An empty plan writes nothing.</summary>
        public static IReadOnlyDictionary<string, string> Settings(ImportPlan plan, string? markerBefore)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var settings = new Dictionary<string, string>(StringComparer.Ordinal);
            if (plan.Items.Count == 0) return settings;
            foreach (var item in plan.Items) settings[item.Setting.Id] = item.Value;
            settings[ThresholdSettings.Internal.ImportedFrom.Id] = ImportMarker.Add(markerBefore, SourceName);
            return settings;
        }

        /// <summary>The preview text: each note in the player's language (<paramref name="noteText"/>), then one
        /// "Set id = value" line per setting Apply writes.</summary>
        public static IReadOnlyList<string> PreviewLines(ImportPlan plan, string? markerBefore, Func<ImportNote, string> noteText)
        {
            if (noteText == null) throw new ArgumentNullException(nameof(noteText));
            return plan.Notes.Select(noteText).Concat(Settings(plan, markerBefore).Select(p => "Set " + p.Key + " = " + p.Value)).ToList();
        }

        public static ImportPlan Plan(string? configText)
        {
            var entries = SourceConfig.Parse(configText);
            var items = new List<ImportItem>();
            var notes = new List<ImportNote>();
            if (entries.Count == 0) return new ImportPlan(items, notes);
            string? Get(string section, string key) =>
                entries.Where(e => e.Section == section && e.Key == key).Select(e => (string?)e.Value).FirstOrDefault();
            void Add(ThresholdSetting setting, string value, string source) => items.Add(new ImportItem(setting, value, source));

            var prefabs = Get("General", "PortalPrefabs");
            if (prefabs != null)
                Add(ThresholdSettings.General.PortalPrefabs, PrefabFilter.IsPortalSelectorDefault(prefabs) ? "" : ThresholdSettings.NameList(prefabs), "General.PortalPrefabs");

            var returnTo = Get("General", "ReturnToMain");
            if (returnTo != null)
            {
                if (Enum.TryParse<ReturnToHubMode>(returnTo, false, out var mode) && Enum.IsDefined(typeof(ReturnToHubMode), mode) && !int.TryParse(returnTo, out _))
                    Add(ThresholdSettings.Hubs.ReturnToHub, mode.ToString(), "General.ReturnToMain");
                else
                    notes.Add(ImportNote.InvalidValue);
            }

            var respect = Bool(Get("General", "RespectTeleportRestrictions"));
            if (respect.HasValue)
            {
                Add(ThresholdSettings.Cheats.IgnoreTeleportRestrictions, respect.Value ? "false" : "true", "General.RespectTeleportRestrictions");
                if (!respect.Value) notes.Add(ImportNote.CheatAppliesWhereAllowed);
            }

            foreach (var (key, setting) in new[] { ("UseGameMap", ThresholdSettings.Map.UseGameMap), ("HideUnexploredMap", ThresholdSettings.Map.HideUnexploredMap), ("ShowMapLabels", ThresholdSettings.Map.ShowMapLabels) })
            {
                var value = Bool(Get("Map", key));
                if (value.HasValue) Add(setting, value.Value ? "true" : "false", "Map." + key);
            }

            if (Get("UI", "Language") != null) notes.Add(ImportNote.LanguageNotImported);
            if (Get("UI", "UseGameFont") != null) notes.Add(ImportNote.FontNotImported);
            notes.Add(ImportNote.HubNotFound);
            return new ImportPlan(items, notes);
        }

        private static bool? Bool(string? value)
        {
            if (value == null) return null;
            if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }
    }
}
