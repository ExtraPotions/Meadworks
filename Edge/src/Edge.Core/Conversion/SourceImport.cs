// Edge - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings (design 10.3), same plan model as Wildblood.Core and Ember.Core (expDARE). Only
// section, key and value names of the source config files are used; no source code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Edge.Config;

namespace ExtraPotions.Edge.Conversion
{
    public enum ImportSource { SecondaryAttacksConfig, SlopeCombatAssistanceConfig, KeepWeaponsAfterSwimConfig }

    public enum ImportNoteKind
    {
        /// <summary>Deliberately not imported (server lock, a value another product's converter takes, a cheat on a client file).</summary>
        Skipped,

        /// <summary>A key Edge does not know.</summary>
        Unknown,

        /// <summary>A number outside Edge's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value or an unknown name; Edge's default is used.</summary>
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

        /// <summary>The normalized value, typed as the Edge setting.</summary>
        public object Value { get; }
        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source, IReadOnlyList<ImportValue> values, IReadOnlyList<ImportNote> notes)
        {
            Source = source;
            Values = values;
            Notes = notes;
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

    /// <summary>Plans an import (C09 Preview): source entries to Edge setting values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.SecondaryAttacksConfig: return "sighsorry.SecondaryAttacks.cfg";
                case ImportSource.SlopeCombatAssistanceConfig: return "digitalroot.mods.slopecombatassistance.cfg";
                case ImportSource.KeepWeaponsAfterSwimConfig: return "lazly.valheim.keepweaponsafterswim.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        private const string SaGeneral = "1 - General";
        private const string SaCheatKey = SaGeneral + ".Admin No Preset Cooldowns";

        private static readonly Dictionary<string, EdgeSetting> Sa = new Dictionary<string, EdgeSetting>(StringComparer.Ordinal)
        {
            { SaGeneral + ".Quickstep Enabled", EdgeSettings.General.QuickstepEnabled },
            { SaGeneral + ".Auto Equip Last Shield", EdgeSettings.Equipment.AutoEquipLastShield },
            { SaCheatKey, EdgeSettings.Cheats.NoSecondaryCooldowns },
            { "2 - Blood Magic.Magic Summon Quality Preset", EdgeSettings.BloodMagic.SummonQualityPreset },
            { "2 - Blood Magic.Blood Magic Summon Lifetime Seconds", EdgeSettings.BloodMagic.SummonLifetimeSeconds },
            { "2 - Blood Magic.Blood Magic Summon Lifetime Multiplier At Skill 100", EdgeSettings.BloodMagic.LifetimeMultiplierAtSkill100 },
            { "2 - Blood Magic.Blood Magic Health Cost Uses Max Health", EdgeSettings.BloodMagic.HealthCostUsesMaxHealth },
            { "2 - Blood Magic.Blood Magic Health Cost Skill Raise Factor", EdgeSettings.BloodMagic.HealthCostSkillRaiseFactor },
            { "3 - Ranged.One-Handed Elemental Weapon Preset", EdgeSettings.Ranged.OneHandedElementalPreset },
            { "3 - Ranged.Fireball Staff Preset", EdgeSettings.Ranged.FireballStaffPreset },
            { "3 - Ranged.Rapidfire Staff Preset", EdgeSettings.Ranged.RapidfireStaffPreset },
            { "3 - Ranged.Reload Staff Preset", EdgeSettings.Ranged.LightningStaffPreset },
            { "3 - Ranged.Bow Preset", EdgeSettings.Ranged.BowPreset },
            { "3 - Ranged.Crossbow Preset", EdgeSettings.Ranged.CrossbowPreset },
            { "3 - Ranged.Bomb Preset", EdgeSettings.Ranged.BombPreset },
            { "4 - UI.Secondary Cooldown HUD Enabled", EdgeSettings.Interface.CooldownHud },
            { "4 - UI.Secondary Attack Tooltips Enabled", EdgeSettings.Interface.SecondaryTooltips },
        };

        private static readonly Dictionary<string, string> SaSkips = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { SaGeneral + ".Lock Configuration", "dropped: Crucible always binds clients to the server's values" },
            { SaGeneral + ".Backstab Sneak Skill Raise Amount", "handed to Saga's converter (Sneak module)" },
            { SaGeneral + ".Sneak Visibility Skill Effect Factor", "handed to Saga's converter (Sneak module)" },
            { SaGeneral + ".Sneak Movement Speed Skill Factor", "handed to Saga's converter (Sneak module)" },
            { SaGeneral + ".Keep Crouching During Elemental Damage Over Time", "handed to Saga's converter (Sneak module)" },
        };

        private static readonly Dictionary<string, EdgeSetting> Sca = new Dictionary<string, EdgeSetting>(StringComparer.Ordinal)
        {
            { "General.Height", EdgeSettings.Slope.AttackHeight },
            { "General.Offset", EdgeSettings.Slope.AttackOffset },
        };

        private static readonly Dictionary<string, EdgeSetting> Kwas = new Dictionary<string, EdgeSetting>(StringComparer.Ordinal)
        {
            { "General.Mode", EdgeSettings.Equipment.SwimMode },
            { "General.RestoreDelay", EdgeSettings.Equipment.RestoreDelay },
        };

        /// <summary>
        /// The plan for one source file. <paramref name="authority"/> is true for a single player or server file: only then
        /// does SecondaryAttacks' Admin No Preset Cooldowns set the No Secondary Cooldowns cheat (design 10.3).
        /// </summary>
        public static ImportPlan Plan(ImportSource source, string? text, bool authority)
        {
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            var (maps, skips) = source == ImportSource.SecondaryAttacksConfig ? (Sa, SaSkips)
                : source == ImportSource.SlopeCombatAssistanceConfig ? (Sca, new Dictionary<string, string>())
                : (Kwas, new Dictionary<string, string>());
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                if (skips.TryGetValue(from, out var why))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, why));
                    continue;
                }
                if (!maps.TryGetValue(from, out var target))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not an Edge setting"));
                    continue;
                }
                if (from == SaCheatKey && !authority)
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "a cheat is imported only from a single player or server file"));
                    continue;
                }
                Add(target, entry.Value, from, values, notes);
            }
            if (source == ImportSource.SlopeCombatAssistanceConfig && values.Count > 0)
                values.Add(new ImportValue(EdgeSettings.Slope.Mode.Id, SlopeMode.Fixed, "Slope Combat Assistance behavior"));
            return new ImportPlan(source, values, notes);
        }

        private static void Add(EdgeSetting target, string text, string from, List<ImportValue> values, List<ImportNote> notes)
        {
            if (values.Any(v => v.SettingId == target.Id))
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "merged into " + target.Id + " (first value kept)"));
                return;
            }
            values.Add(new ImportValue(target.Id, Convert(target, text, from, notes), from));
        }

        private static object Convert(EdgeSetting target, string text, string from, List<ImportNote> notes)
        {
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Edge default " + ImportPlan.Format(target.DefaultValue)));
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
                case EdgeSetting<bool> _:
                    if (Eq(text, "true") || Eq(text, "On")) return true;
                    if (Eq(text, "false") || Eq(text, "Off")) return false;
                    return Fallback("unreadable \"" + text + "\"");
                case EdgeSetting<float> _:
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                        ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case EdgeSetting<int> _:
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? Checked(i) : Fallback("unreadable \"" + text + "\"");
                case EdgeSetting<string> _:
                    return Checked(text);
                default:
                    var name = text.Replace(" ", "");
                    if (target.ValueType.IsEnum && target.Choices.Any(c => Eq(c, name)))
                        return Enum.Parse(target.ValueType, name, true);
                    return Fallback("unknown value \"" + text + "\"");
            }
        }

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
    }
}
