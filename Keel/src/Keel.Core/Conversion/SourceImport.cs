// Keel - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings from ShipConfig/GrindstoneSkills config names and the pinned
// TheGreatestShips README (design 9.2). No unlicensed implementation read.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Keel.Config;
using ExtraPotions.Keel.Tuning;

namespace ExtraPotions.Keel.Conversion
{
    public enum ImportSource { ShipConfig, GrindstoneSkills, TheGreatestShips }
    public enum ShipImportUnit { Absolute, BaseHullSpeedRatio, BaseHullRudderRatio }

    public enum ImportNoteKind
    {
        /// <summary>Section or key that Keel does not import (server sync lock, unknown field, vanilla Ashlands ship).</summary>
        Skipped,

        /// <summary>A number outside Keel's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value; Keel's default is used, or a per-ship value is left out.</summary>
        Fallback,

        /// <summary>A cheat the import turns on because the source made ships immune or invulnerable.</summary>
        Cheat,

        /// <summary>A GrindstoneSkills section another Meadworks product imports (reported once per section).</summary>
        OtherProduct,
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

        /// <summary><c>Section.Key</c> (or the section) in the source file.</summary>
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

        /// <summary>The normalized value, typed as the Keel setting (bool, float or string).</summary>
        public object Value { get; }

        public string Source { get; }
    }

    /// <summary>One per-ship value (design 4.2), clamped to that ship's range when applied (the range needs the prefab's defaults).</summary>
    public sealed class ShipImportValue
    {
        internal ShipImportValue(string prefab, ShipField field, float value, string source, ShipImportUnit unit = ShipImportUnit.Absolute)
        {
            Prefab = prefab;
            Field = field;
            Value = value;
            Source = source;
            Unit = unit;
        }

        public string Prefab { get; }
        public ShipField Field { get; }

        /// <summary>The number, or 0 and 1 for a switch.</summary>
        public float Value { get; }

        public string Source { get; }

        public ShipImportUnit Unit { get; }
        public float Resolve(float baseHullValue)
        {
            if (Unit == ShipImportUnit.Absolute) return Value;
            if (baseHullValue <= 0 || float.IsNaN(baseHullValue) || float.IsInfinity(baseHullValue))
                throw new InvalidOperationException("Native base hull facts are not available; preview after vanilla prefabs load.");
            var resolved = baseHullValue * Value * (Unit == ShipImportUnit.BaseHullSpeedRatio ? Value : 1f);
            if (float.IsNaN(resolved) || float.IsInfinity(resolved)) throw new InvalidOperationException("Source ship ratio exceeds native range.");
            return resolved;
        }

        public string Text => ShipFields.Get(Field).IsToggle ? (Value != 0f ? "true" : "false") : ImportPlan.Format(Value);
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source, IReadOnlyList<ImportValue> values, IReadOnlyList<ShipImportValue> shipValues, IReadOnlyList<ImportNote> notes)
        {
            Source = source;
            Values = values;
            ShipValues = shipValues;
            Notes = notes;
        }

        public ImportSource Source { get; }
        public IReadOnlyList<ImportValue> Values { get; }
        public IReadOnlyList<ShipImportValue> ShipValues { get; }
        public IReadOnlyList<ImportNote> Notes { get; }

        /// <summary>Plain text preview report, one line per value and note.</summary>
        public string Report() =>
            string.Join("\n", new[] { "Source: " + Source }
                .Concat(Values.Select(v => "Set " + v.SettingId + " = " + Format(v.Value) + " (from " + v.Source + ")"))
                .Concat(ShipValues.Select(v => "Set " + ShipFields.Section + "." + ShipFields.Key(v.Prefab, v.Field) + " = " + v.Text
                    + (v.Unit == ShipImportUnit.Absolute ? "" : " " + v.Unit + " (resolved against native hull during preview)") + " (from " + v.Source + ")"))
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

    /// <summary>Plans an import: source entries to Keel setting and per-ship values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        /// <summary>Character custom data key set once the GrindstoneSkills Sailing level was moved (design 9.2).</summary>
        public const string SkillImportMarker = "Keel.imported.sailing";

        /// <summary>The vanilla Ashlands-ready ship; ShipConfig's AshlandsOceanDamage = false on it changes nothing.</summary>
        public const string VanillaAshlandsShip = "VikingShip_Ashlands";

        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.ShipConfig: return "com.ShipConfig.cfg";
                case ImportSource.GrindstoneSkills: return "com.GrindstoneSkills.cfg";
                case ImportSource.TheGreatestShips: return "DeathMonger.TheGreatestShips.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        private static readonly string[] GsSections = { "8 - Sailing", "9 - Lookout", "35 - Wind Call" };

        private static readonly Dictionary<string, KeelSetting> Gs = new Dictionary<string, KeelSetting>(StringComparer.Ordinal)
        {
            { "8 - Sailing.Sailing Enabled", KeelSettings.Sailing.Enabled },
            { "8 - Sailing.Ship Health At 100", KeelSettings.Sailing.ShipHealthAt100 },
            { "8 - Sailing.Ship Speed At 100", KeelSettings.Sailing.ShipSpeedAt100 },
            { "8 - Sailing.Exploration Radius At 100", KeelSettings.Sailing.ExplorationRadiusAt100 },
            { "8 - Sailing.Helm Experience Per Kilometre", KeelSettings.Sailing.HelmExperiencePerKilometre },
            { "8 - Sailing.Crew Experience Share", KeelSettings.Sailing.CrewExperienceShare },
            { "9 - Lookout.Lookout Level", KeelSettings.Lookout.Level },
            { "9 - Lookout.Lookout Radius", KeelSettings.Lookout.Radius },
            { "9 - Lookout.Lookout Duration", KeelSettings.Lookout.Duration },
            { "9 - Lookout.Lookout Cooldown", KeelSettings.Lookout.Cooldown },
            { "9 - Lookout.Lookout Key", KeelSettings.Lookout.Key },
            { "35 - Wind Call.Wind Call Level", KeelSettings.WindCall.Level },
            { "35 - Wind Call.Wind Call Duration", KeelSettings.WindCall.Duration },
            { "35 - Wind Call.Wind Call Cooldown", KeelSettings.WindCall.Cooldown },
            { "35 - Wind Call.Wind Call Key", KeelSettings.WindCall.Key },
        };

        private static readonly Dictionary<string, KeelSetting> Sc = new Dictionary<string, KeelSetting>(StringComparer.Ordinal)
        {
            { "Multipliers.Health Multiplier", KeelSettings.Multipliers.Health },
            { "Multipliers.Sail Force Multiplier", KeelSettings.Multipliers.SailForce },
            { "Multipliers.Paddle Force Multiplier", KeelSettings.Multipliers.PaddleForce },
            { "Multipliers.Turning Multiplier", KeelSettings.Multipliers.Turning },
            { "Multipliers.Damage Taken Multiplier", KeelSettings.Multipliers.DamageTaken },
            { "Multipliers.Build Cost Multiplier", KeelSettings.Multipliers.BuildCost },
            { "Display.Ship Panel", KeelSettings.Display.ShipPanel },
        };

        public static ImportPlan Plan(ImportSource source, string? text)
        {
            var values = new List<ImportValue>();
            var ships = new List<ShipImportValue>();
            var notes = new List<ImportNote>();
            var immune = new List<string>();
            var invulnerable = new List<string>();
            var otherSections = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                if (source == ImportSource.TheGreatestShips)
                {
                    TgsImport.Entry(entry, values, ships, notes);
                    continue;
                }
                if (source == ImportSource.GrindstoneSkills && !GsSections.Contains(entry.Section))
                {
                    if (otherSections.Add(entry.Section))
                        notes.Add(new ImportNote(ImportNoteKind.OtherProduct, entry.Section, "imported by another Meadworks product"));
                    continue;
                }
                if (source == ImportSource.ShipConfig && entry.Section == "Ship")
                {
                    PlanShip(entry, from, ships, notes, immune, invulnerable);
                    continue;
                }
                var map = source == ImportSource.ShipConfig ? Sc : Gs;
                if (!map.TryGetValue(from, out var target))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "not imported"));
                    continue;
                }
                if (values.Any(v => v.SettingId == target.Id)) continue;
                values.Add(new ImportValue(target.Id, Convert(target, entry.Value, from, notes), from));
            }
            AddCheat(values, notes, KeelSettings.Cheats.AshlandsImmuneShips, KeelSettings.Cheats.AshlandsImmuneList, immune, "AshlandsOceanDamage = false");
            AddCheat(values, notes, KeelSettings.Cheats.InvulnerableShips, KeelSettings.Cheats.InvulnerableList, invulnerable, "Invulnerable = true");
            return new ImportPlan(source, values, ships, notes);
        }

        /// <summary>
        /// The builder level of a ship: Keel's own ZDO key when set, else GrindstoneSkills' (read only, never written);
        /// negative when neither is set.
        /// </summary>
        public static float ShipwrightLevel(float keelValue, float sourceValue) => keelValue >= 0f ? keelValue : sourceValue;

        private static void PlanShip(SourceEntry entry, string from, List<ShipImportValue> ships, List<ImportNote> notes, List<string> immune, List<string> invulnerable)
        {
            var dot = entry.Key.LastIndexOf('.');
            var prefab = dot > 0 ? entry.Key.Substring(0, dot).Trim() : "";
            var name = dot > 0 ? entry.Key.Substring(dot + 1).Trim() : "";
            if (prefab.Length == 0 || name.Length == 0)
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "not Prefab.Field"));
                return;
            }
            if (name == "AshlandsOceanDamage" || name == "Invulnerable")
            {
                if (!TryBool(entry.Value, out var flag))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "unreadable \"" + entry.Value + "\"; not imported"));
                    return;
                }
                if (name == "AshlandsOceanDamage" && !flag)
                {
                    if (prefab == VanillaAshlandsShip) notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "the game already spares this ship"));
                    else if (!immune.Contains(prefab)) immune.Add(prefab);
                }
                if (name == "Invulnerable" && flag && !invulnerable.Contains(prefab)) invulnerable.Add(prefab);
                return;
            }
            if (!Enum.TryParse<ShipField>(name, false, out var field) || !Enum.IsDefined(typeof(ShipField), field) || name != field.ToString())
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "unknown ship field"));
                return;
            }
            float value;
            if (ShipFields.Get(field).IsToggle)
            {
                if (!TryBool(entry.Value, out var on))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "unreadable \"" + entry.Value + "\"; not imported"));
                    return;
                }
                value = on ? 1f : 0f;
            }
            else if (!TryFloat(entry.Value, out value))
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "unreadable \"" + entry.Value + "\"; not imported"));
                return;
            }
            ships.RemoveAll(s => s.Prefab == prefab && s.Field == field);
            ships.Add(new ShipImportValue(prefab, field, value, from));
        }

        private static void AddCheat(List<ImportValue> values, List<ImportNote> notes, KeelSetting<bool> cheat, KeelSetting<string> list, List<string> ships, string why)
        {
            if (ships.Count == 0) return;
            var text = list.Normalize(string.Join(",", ships));
            values.Add(new ImportValue(cheat.Id, true, "Ship.*." + why.Split(' ')[0]));
            values.Add(new ImportValue(list.Id, text, "Ship.*." + why.Split(' ')[0]));
            notes.Add(new ImportNote(ImportNoteKind.Cheat, cheat.Id, "turned on for " + text + " (ShipConfig " + why + ")"));
        }

        internal static object Convert(KeelSetting target, string text, string from, List<ImportNote> notes)
        {
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Keel default " + ImportPlan.Format(target.DefaultValue)));
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
                case KeelSetting<bool> _:
                    return TryBool(text, out var b) ? b : Fallback("unreadable \"" + text + "\"");
                case KeelSetting<float> _:
                    return TryFloat(text, out var f) ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case KeelSetting<int> _:
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? Checked(i) : Fallback("unreadable \"" + text + "\"");
                case KeelSetting<string> _:
                    return Checked(text);
                default:
                    return Fallback("unsupported type");
            }
        }

        private static bool TryBool(string text, out bool value)
        {
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
            value = false;
            return false;
        }

        private static bool TryFloat(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
