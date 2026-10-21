// Cairn - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings (design 9.2 with the plain-toggle decision), same plan model as Edge.Core,
// Wildblood.Core and Ember.Core (expDARE). Only section, key and value names of the source config files are used.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Cairn.Config;
using ExtraPotions.Cairn.Visibility;
using static ExtraPotions.Cairn.Visibility.VisibilityEffect;

namespace ExtraPotions.Cairn.Conversion
{
    public enum ImportSource { PathfinderConfig, NoFogBruhConfig, BetterFogConfig, TimeWeatherHudConfig }

    public enum ImportNoteKind
    {
        /// <summary>Deliberately not imported (another product's converter takes it, telemetry, language).</summary>
        Skipped,

        /// <summary>A key Cairn does not know.</summary>
        Unknown,

        /// <summary>A number outside Cairn's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value or an unknown name; Cairn's default is used.</summary>
        Fallback,

        /// <summary>A graded source value (between 0 and 1), imported as the matching Visibility Strength setting.</summary>
        Graded,

        /// <summary>Two sources set the same setting; how they were merged.</summary>
        Merged,
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

        /// <summary>The normalized value, typed as the Cairn setting.</summary>
        public object Value { get; }
        public string Source { get; }
    }

    public sealed class ImportPlan
    {
        internal ImportPlan(string source, IReadOnlyList<ImportValue> values, IReadOnlyList<ImportNote> notes)
        {
            Source = source;
            Values = values;
            Notes = notes;
        }

        public string Source { get; }
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

    /// <summary>Plans an import (C09 Preview): source entries to Cairn setting values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        public const string MarkersFileName = "lazly.valheim.timeweatherhud.markers.txt";

        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.PathfinderConfig: return "dev.crystal.pathfinder.cfg";
                case ImportSource.NoFogBruhConfig: return "vapok.mods.nofogbruh.cfg";
                case ImportSource.BetterFogConfig: return "JevMods.BetterFog.cfg";
                case ImportSource.TimeWeatherHudConfig: return "lazly.valheim.timeweatherhud.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        private static readonly Dictionary<string, CairnSetting> Pf = new Dictionary<string, CairnSetting>(StringComparer.Ordinal)
        {
            { "Base.MinimumRadius", CairnSettings.Radius.MinimumRadius },
            { "Base.MaximumRadius", CairnSettings.Radius.MaximumRadius },
            { "Base.LandExploreRadius", CairnSettings.Radius.LandExploreRadius },
            { "Base.SeaExploreRadius", CairnSettings.Radius.SeaExploreRadius },
            { "Multipliers.AltitudeRadiusBonus", CairnSettings.Radius.AltitudeRadiusBonus },
            { "Multipliers.ForestRadiusPenalty", CairnSettings.Radius.ForestRadiusPenalty },
            { "Multipliers.DaylightRadiusScale", CairnSettings.Radius.DaylightRadiusScale },
            { "Multipliers.WeatherRadiusScale", CairnSettings.Radius.WeatherRadiusScale },
            { "Miscellaneous.DisplayCurrentRadiusValue", CairnSettings.Radius.ShowRadius },
            { "Miscellaneous.DisplayVariables", CairnSettings.Radius.ShowRadiusInputs },
        };

        /// <summary>NoFogBruh visual switches ("Enable X"; false removes X) and whether NoFogBruh syncs the switch with a server.</summary>
        private static readonly Dictionary<string, (VisibilityEffect Effect, bool Synced)> Nf = new Dictionary<string, (VisibilityEffect, bool)>(StringComparer.Ordinal)
        {
            { "General.Enable Fog Component", (DistanceFog, false) },
            { "General.Enable Distant Fog", (DistanceFog, false) },
            { "General.Enable Ambient Occlusion Component", (AmbientOcclusion, false) },
            { "General.Enable Ground Mist", (GroundMist, false) },
            { "General.Enable Fog Clouds", (FogClouds, false) },
            { "General.Enable Mist Emitters", (MistEmitters, false) },
            { "Ocean.Enable Ocean Mist", (OceanMist, false) },
            { "Black Forest.Enable Troll Cave Mist", (CaveMist, true) },
            { "Mountain.Enable Mountain Cave Mist", (CaveMist, true) },
            { "Mountain.Enable Snow Glint", (SnowGlint, false) },
            { "Mountain.Enable Snowstorm / Blizzard Fog", (SnowstormFog, false) },
            { "Mistlands.Enable Mistlands Global Mist", (MistlandsMist, true) },
            { "Mistlands.Enable Particle Mist", (ParticleMist, true) },
            { "Ashlands.Enable Ashlands Global Mist", (AshlandsMist, false) },
            { "Ashlands.Enable Ashlands FaderFX", (AshlandsFader, false) },
            { "Deep North.Enable Deep North Global Mist", (DeepNorthMist, false) },
            { "Deep North.Enable Deep North FaderFX", (DeepNorthFader, false) },
        };

        /// <summary>BetterFog fractions of vanilla (0 none, 1 vanilla) and the effects each feeds.</summary>
        private static readonly Dictionary<string, VisibilityEffect[]> Bf = new Dictionary<string, VisibilityEffect[]>(StringComparer.Ordinal)
        {
            { "Fog.Density", new[] { DistanceFog } },
            { "Fog.MistOpacity", new[] { MistlandsMist, ParticleMist } },
            { "Smoke.Opacity", new[] { Smoke } },
        };

        /// <summary>The Visibility Strength setting a graded BetterFog fraction becomes.</summary>
        private static readonly Dictionary<string, CairnSetting<float>> BfStrength = new Dictionary<string, CairnSetting<float>>(StringComparer.Ordinal)
        {
            { "Fog.Density", CairnSettings.VisibilityStrength.DistanceFog },
            { "Fog.MistOpacity", CairnSettings.VisibilityStrength.Mist },
            { "Smoke.Opacity", CairnSettings.VisibilityStrength.Smoke },
        };

        private static readonly Dictionary<string, CairnSetting> Tw = new Dictionary<string, CairnSetting>(StringComparer.Ordinal)
        {
            { "General.Enabled", CairnSettings.Hud.Enabled },
            { "General.ToggleKey", CairnSettings.Hud.ToggleKey },
            { "Display.ShowDay", CairnSettings.Hud.ShowDay },
            { "Display.ShowTime", CairnSettings.Hud.ShowTime },
            { "Display.ShowWeather", CairnSettings.Hud.ShowWeather },
            { "Display.Use24Hour", CairnSettings.Hud.Use24Hour },
            { "Compass.ShowCompass", CairnSettings.Hud.ShowCompass },
            { "Compass.StripDegrees", CairnSettings.Hud.StripDegrees },
            { "Compass.ShowHeading", CairnSettings.Hud.ShowHeading },
            { "Markers.ShowMarkers", CairnSettings.Hud.ShowMarkers },
            { "Markers.MarkerMenuKey", CairnSettings.Hud.SettingsKey },
            { "Markers.MaxMarkers", CairnSettings.Hud.MaxMarkers },
            { "Markers.MaxDistance", CairnSettings.Hud.MaxDistance },
        };

        /// <summary>
        /// The plan for one source file. <paramref name="serverFile"/> is true when converting a dedicated server's or host's
        /// file: only then do NoFogBruh's server-synced switches that keep mist become "Allow Removing" off (design 9.2).
        /// </summary>
        public static ImportPlan Plan(ImportSource source, string? text, bool serverFile)
        {
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                switch (source)
                {
                    case ImportSource.PathfinderConfig: Simple(Pf, from, entry.Value, values, notes); break;
                    case ImportSource.TimeWeatherHudConfig:
                        if (entry.Section == "UI") notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "Cairn follows the game language and always uses the game font through the kit"));
                        else Simple(Tw, from, entry.Value, values, notes);
                        break;
                    case ImportSource.NoFogBruhConfig: NoFogBruh(from, entry, serverFile, values, notes); break;
                    case ImportSource.BetterFogConfig: BetterFog(from, entry.Value, values, notes); break;
                    default: throw new ArgumentException("Unknown source.", nameof(source));
                }
            }
            return new ImportPlan(source.ToString(), values, notes);
        }

        /// <summary>
        /// Several plans as one (NoFogBruh and BetterFog may both be present): a removal from any source wins (the lower value
        /// wins, design 9.2), an "Allow Removing" off from any server source wins, other settings keep the first value.
        /// </summary>
        public static ImportPlan Combine(IEnumerable<ImportPlan> plans)
        {
            var list = (plans ?? throw new ArgumentNullException(nameof(plans))).ToList();
            var values = new List<ImportValue>();
            var notes = list.SelectMany(p => p.Notes).ToList();
            foreach (var value in list.SelectMany(p => p.Values))
            {
                var index = values.FindIndex(v => v.SettingId == value.SettingId);
                if (index < 0) { values.Add(value); continue; }
                var existing = values[index];
                if (IsRemove(value.SettingId) ? (bool)value.Value && !(bool)existing.Value : IsAllow(value.SettingId) && !(bool)value.Value && (bool)existing.Value)
                {
                    values[index] = value;
                    notes.Add(new ImportNote(ImportNoteKind.Merged, value.Source, value.SettingId + " " + ImportPlan.Format(value.Value) + " wins over " + existing.Source));
                }
                else
                {
                    notes.Add(new ImportNote(ImportNoteKind.Merged, value.Source, value.SettingId + " keeps " + ImportPlan.Format(existing.Value) + " from " + existing.Source));
                }
            }
            return new ImportPlan(string.Join(" + ", list.Select(p => p.Source)), values, notes);
        }

        private static bool IsRemove(string id) => id.StartsWith(CairnSettings.Visibility.Section + ".", StringComparison.Ordinal);

        private static bool IsAllow(string id) => id.StartsWith(CairnSettings.VisibilityServer.Section + ".", StringComparison.Ordinal);

        private static void NoFogBruh(string from, SourceEntry entry, bool serverFile, List<ImportValue> values, List<ImportNote> notes)
        {
            if (entry.Section.StartsWith("Weather Events", StringComparison.Ordinal))
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "weather events are handed to Tempest's converter (Cairn Q2)"));
                return;
            }
            if (entry.Section == "Local Config" || entry.Section == "Log Output Configuration")
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "NoFogBruh's own splash, telemetry and logging; not imported"));
                return;
            }
            if (!Nf.TryGetValue(from, out var map))
            {
                notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not a Cairn setting"));
                return;
            }
            if (!TryBool(entry.Value, out var enabled))
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "unreadable \"" + entry.Value + "\"; effect kept"));
                return;
            }
            Remove(map.Effect, !enabled, from, values);
            if (serverFile && map.Synced && enabled)
                SetOnce(CairnSettings.VisibilityServer.Allow(map.Effect), false, from, values);
        }

        private static void BetterFog(string from, string text, List<ImportValue> values, List<ImportNote> notes)
        {
            if (!Bf.TryGetValue(from, out var effects))
            {
                notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not a Cairn setting"));
                return;
            }
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) || float.IsNaN(fraction) || float.IsInfinity(fraction))
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, "unreadable \"" + text + "\"; effect kept"));
                return;
            }
            var remove = fraction <= 0f;
            foreach (var effect in effects) Remove(effect, remove, from, values);
            if (!remove && fraction < 1f)
            {
                var strength = BfStrength[from];
                SetOnce(strength, Math.Min(1f, fraction), from, values);
                notes.Add(new ImportNote(ImportNoteKind.Graded, from, ImportPlan.Format(fraction) + " of vanilla imported as " + strength.Key));
            }
        }

        /// <summary>A removal toggle fed by one or more source switches: removed when any feeding switch removes it.</summary>
        private static void Remove(VisibilityEffect effect, bool remove, string from, List<ImportValue> values)
        {
            var id = CairnSettings.Visibility.Remove(effect).Id;
            var index = values.FindIndex(v => v.SettingId == id);
            if (index < 0) values.Add(new ImportValue(id, remove, from));
            else if (remove && !(bool)values[index].Value) values[index] = new ImportValue(id, true, from);
        }

        private static void SetOnce(CairnSetting target, object value, string from, List<ImportValue> values)
        {
            if (!values.Any(v => v.SettingId == target.Id)) values.Add(new ImportValue(target.Id, value, from));
        }

        private static void Simple(Dictionary<string, CairnSetting> map, string from, string text, List<ImportValue> values, List<ImportNote> notes)
        {
            if (!map.TryGetValue(from, out var target))
            {
                notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not a Cairn setting"));
                return;
            }
            if (values.Any(v => v.SettingId == target.Id))
            {
                notes.Add(new ImportNote(ImportNoteKind.Merged, from, "merged into " + target.Id + " (first value kept)"));
                return;
            }
            values.Add(new ImportValue(target.Id, Convert(target, text, from, notes), from));
        }

        private static object Convert(CairnSetting target, string text, string from, List<ImportNote> notes)
        {
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Cairn default " + ImportPlan.Format(target.DefaultValue)));
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
                case CairnSetting<bool> _:
                    return TryBool(text, out var b) ? b : Fallback("unreadable \"" + text + "\"");
                case CairnSetting<float> _:
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                        ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case CairnSetting<int> _:
                    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)) return Checked(i);
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var whole) && !float.IsNaN(whole) && !float.IsInfinity(whole)
                        ? Checked((int)Math.Round(whole, MidpointRounding.AwayFromZero)) : Fallback("unreadable \"" + text + "\"");
                case CairnSetting<string> _:
                    return Checked(text);
                default:
                    var name = text.Replace(" ", "");
                    if (target.ValueType.IsEnum && target.Choices.Any(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase)))
                        return Enum.Parse(target.ValueType, name, true);
                    return Fallback("unknown value \"" + text + "\"");
            }
        }

        private static bool TryBool(string text, out bool value)
        {
            var t = (text ?? "").Trim();
            if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) { value = true; return true; }
            if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) { value = false; return true; }
            value = false;
            return false;
        }
    }
}
