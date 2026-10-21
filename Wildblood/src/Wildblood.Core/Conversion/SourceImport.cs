// Wildblood - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter mappings (design 11.2), same plan model as Ember.Core (expDARE). Only section, key and value
// names of the source files are used; no source code.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ExtraPotions.Wildblood.Config;
using ExtraPotions.Wildblood.Loot;
using ExtraPotions.Wildblood.Stars;
using ExtraPotions.Wildblood.Traits;

namespace ExtraPotions.Wildblood.Conversion
{
    public enum ImportSource { EliteCreaturesRebornConfig, EliteCreaturesRebornRules, CreatureManagerConfig, SaveYourLuckBruhConfig }

    public enum ImportNoteKind
    {
        /// <summary>Deliberately not imported (server lock, a value another product's converter takes, a later unit).</summary>
        Skipped,

        /// <summary>A key Wildblood does not know (reported Unknown, design 11.2).</summary>
        Unknown,

        /// <summary>A number outside Wildblood's range, clamped.</summary>
        Clamped,

        /// <summary>An unreadable value or an unknown name; Wildblood's default is used.</summary>
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

        /// <summary>The normalized value, typed as the Wildblood setting.</summary>
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

    /// <summary>Plans an import (C09 Preview): source entries to Wildblood setting values, with clamps, fallbacks and skips listed.</summary>
    public static class SourceImport
    {
        /// <summary>One mapping: the Wildblood setting and an optional text conversion (null result = unreadable).</summary>
        private sealed class Map
        {
            public Map(WildbloodSetting target, Func<string, object?>? convert = null)
            {
                Target = target;
                Convert = convert;
            }

            public WildbloodSetting Target { get; }
            public Func<string, object?>? Convert { get; }
        }

        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.EliteCreaturesRebornConfig: return "gglasgow.elitecreaturesreborn.cfg";
                case ImportSource.EliteCreaturesRebornRules: return "creature_rules.yml";
                case ImportSource.CreatureManagerConfig: return "sighsorry.CreatureManager.cfg";
                case ImportSource.SaveYourLuckBruhConfig: return "vapok.mods.SaveYourLuckBruh.cfg";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        private const string EcrDisplay = "8 - Display (per player)";
        private const string EcrRecap = "10 - Death Recap (per player)";
        private const string EcrPalette = "9 - Palette (per player)";

        private static readonly Dictionary<string, Map> Ecr = new Dictionary<string, Map>(StringComparer.Ordinal)
        {
            { "13 - Horde.Horde", new Map(WildbloodSettings.Horde.Enabled) },
            { "13 - Horde.Spawn Multiplier", new Map(WildbloodSettings.Horde.SpawnMultiplier) },
            { "13 - Horde.Creature Health Multiplier", new Map(WildbloodSettings.Horde.HealthMultiplier) },
            { "13 - Horde.Creature Attack Multiplier", new Map(WildbloodSettings.Horde.AttackMultiplier) },
            { EcrDisplay + ".Show trait names", new Map(WildbloodSettings.Display.ShowTraitNames) },
            { EcrDisplay + ".Coloured stars", new Map(WildbloodSettings.Display.ColouredStars) },
            { EcrDisplay + ".Nameplate distance", new Map(WildbloodSettings.Display.NameplateDistance) },
            { EcrDisplay + ".Effect density", new Map(WildbloodSettings.Display.EffectDensity) },
            { EcrDisplay + ".Small star size", new Map(WildbloodSettings.Display.SmallStarSize) },
            { EcrDisplay + ".Large star size", new Map(WildbloodSettings.Display.LargeStarSize) },
            { EcrDisplay + ".Show stolen items", new Map(WildbloodSettings.Display.ShowStolenItems) },
            { EcrDisplay + ".Stolen item icon size", new Map(WildbloodSettings.Display.IconSize) },
            { EcrDisplay + ".Show devoured creatures", new Map(WildbloodSettings.Display.ShowDevouredCreatures) },
            { EcrDisplay + ".Devoured creature icon size", new Map(WildbloodSettings.Display.IconSize) },
            { EcrDisplay + ".Boss damage board seconds", new Map(WildbloodSettings.Display.DamageBoardSeconds) },
            { EcrDisplay + ".Show world tier under minimap", new Map(WildbloodSettings.Display.ShowKarmaUnderMinimap) },
            { EcrDisplay + ".Boss hints", new Map(WildbloodSettings.Display.BossHints) },
            { EcrRecap + ".Record deaths", new Map(WildbloodSettings.Recap.RecordDeaths) },
            { EcrRecap + ".Recap key", new Map(WildbloodSettings.Recap.RecapKey) },
            { EcrRecap + ".Death notice", new Map(WildbloodSettings.Recap.DeathNotice) },
            { EcrRecap + ".Deaths kept", new Map(WildbloodSettings.Recap.DeathsKept) },
            { EcrRecap + ".Seconds before death", new Map(WildbloodSettings.Recap.SecondsBeforeDeath) },
            { EcrRecap + ".Video frames per second", new Map(WildbloodSettings.Recap.VideoFramesPerSecond) },
            { EcrRecap + ".Video height", new Map(WildbloodSettings.Recap.VideoHeight) },
        };

        private static readonly Dictionary<string, string> EcrSkips = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "1 - General.Lock to server", "dropped: Crucible always binds clients to the server's values" },
            { EcrDisplay + ".Boss damage board", "per-player value not imported; the server setting is 5 - Bosses.Damage Board" },
            { "9 - Diagnostics.Log diagnostics", "true becomes the C06 log override wildblood=Verbose; not a Wildblood setting" },
        };

        private static readonly Dictionary<string, Map> Cm = new Dictionary<string, Map>(StringComparer.Ordinal)
        {
            { "7 - AI.Player Target Switching", new Map(WildbloodSettings.AI.Enabled, t => Eq(t,"On") ? true : Eq(t,"Off") ? (object)false : null) },
            { "7 - AI.Player Target Switch Interval (s)", new Map(WildbloodSettings.AI.Interval) },
            { "7 - AI.Player Target Switch Chance (%)", new Map(WildbloodSettings.AI.Chance) },
            { "7 - AI.Player Target Hold Duration (s)", new Map(WildbloodSettings.AI.Hold) },
            { "1 - General.Normal Creature Nameplate Range", new Map(WildbloodSettings.Display.NameplateDistance) },
            { "1 - General.Show Sneak Hover Resistances", new Map(WildbloodSettings.Display.SneakInspect) },
            { "1 - General.Modifier HUD Icon Layout", new Map(WildbloodSettings.Display.ModifierIconLayout) },
            { "1 - General.Block Nearby Spawns While Boss Is Active", new Map(WildbloodSettings.Bosses.BlockSpawnsNearBoss) },
            { "1 - General.Block Nearby Spawns While Enforcer Is Active", new Map(WildbloodSettings.Pressure.BlockSpawnsNearEnforcer) },
            { "2 - Levels.Enable Level System", new Map(WildbloodSettings.Stars.StarMode, LevelSystem) },
            { "2 - Levels.Biome Level Preset", new Map(WildbloodSettings.Stars.Difficulty, t => DifficultyNames.TryParse(t, out var d) && d != Difficulty.Custom ? (object)d : null) },
            { "2 - Levels.Apply Level Scale To Saddle-able Creatures", new Map(WildbloodSettings.Stars.ScaleSaddleable) },
            { "2 - Levels.Global Modifiers", new Map(WildbloodSettings.Traits.CreatureModifiers) },
            { "2 - Levels.Boss Modifiers", new Map(WildbloodSettings.Bosses.BossModifiers) },
            { "3 - Karma.Karma System Mode", new Map(WildbloodSettings.Pressure.KarmaMode) },
            { "3 - Karma.Maximum Enforcers Per Sector", new Map(WildbloodSettings.Pressure.MaxEnforcersPerSector) },
            { "3 - Karma.Dungeon Enforcer Spawn Delay (s)", new Map(WildbloodSettings.Pressure.DungeonEnforcerDelay) },
            { "3 - Karma.Enforcer Abandonment Despawn Time (s)", new Map(WildbloodSettings.Pressure.EnforcerAbandonDespawn) },
            { "3 - Karma.Block Enforcer While Boss Is Active", new Map(WildbloodSettings.Pressure.BlockEnforcerWhileBossActive) },
            { "3 - Karma.Block Karma Gain While Boss Is Active", new Map(WildbloodSettings.Pressure.BlockKarmaWhileBossActive) },
            { "3 - Karma.Block Karma Gain While Enforcer Is Active", new Map(WildbloodSettings.Pressure.BlockKarmaWhileEnforcerActive) },
            { "3 - Karma.Block Omen Enforcer During Cooldown", new Map(WildbloodSettings.Pressure.BlockOmenDuringCooldown) },
            { "4 - Multiplayer Difficulty.HP Increase Per Player In Multiplayer (%)", new Map(WildbloodSettings.Pressure.HealthPerExtraPlayer) },
            { "4 - Multiplayer Difficulty.DMG Increase Per Player In Multiplayer (%)", new Map(WildbloodSettings.Pressure.DamagePerExtraPlayer) },
            { "4 - Multiplayer Difficulty.Maximum Player Count For Multiplayer Scaling", new Map(WildbloodSettings.Pressure.MaxPlayersForScaling) },
            { "5 - Modifiers.Blink Alert Grace Period (s)", new Map(WildbloodSettings.Traits.BlinkAlertGrace) },
            { "5 - Modifiers.Reflection Damage Cap", new Map(WildbloodSettings.Traits.ReflectionDamageCap) },
            { "6 - Loot.Character Loot System", new Map(WildbloodSettings.Loot.Mode, t => Eq(t, "CalculateChance") ? LootMode.Amount : Eq(t, "Vanilla") ? (object)LootMode.Vanilla : null) },
            { "6 - Loot.Chance For Additional Loot Per Star For Creatures", new Map(WildbloodSettings.Loot.AmountPerStar) },
            { "6 - Loot.Chance For Additional Loot Per Star For Bosses", new Map(WildbloodSettings.Loot.AmountPerStarBosses) },
        };

        private static readonly Dictionary<string, string> CmSkips = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "1 - General.Lock Configuration", "dropped: Crucible always binds clients to the server's values" },
            { "2 - Levels.Bosses Follow Biome Level Preset", "bosses roll their own star table (design D3)" },
            { "2 - Levels.Enforcer Modifiers", "Enforcer modifier tuples move to the rule file (B12)" },
            { "3 - Karma.Show Karma Value On Minimap", "shown with 10 - Display.Show Karma Under Minimap" },
        };

        private static readonly Dictionary<string, Map> Sylb = new Dictionary<string, Map>(StringComparer.Ordinal)
        {
            { "Local Settings.Enable SaveYourLuckBruh", new Map(WildbloodSettings.Luck.PersistCounters) },
        };

        private static readonly Dictionary<string, Map> EcrRules = new Dictionary<string, Map>(StringComparer.Ordinal)
        {
            { "difficulty", new Map(WildbloodSettings.Stars.Difficulty, t => DifficultyNames.TryParse(t, out var d) ? (object)d : null) },
            { "creature stars", new Map(WildbloodSettings.Stars.StarMode, t => Eq(t, "true") ? StarMode.Wildblood : Eq(t, "false") ? (object)StarMode.KeepExisting : null) },
            { "max mutations", new Map(WildbloodSettings.Traits.MaxMutations) },
            { "world tiers.enabled", new Map(WildbloodSettings.WorldTier.Enabled) },
            { "world tiers.bosses", new Map(WildbloodSettings.WorldTier.BossKeys, t => string.Join(", ", SimpleYaml.List(t))) },
            { "bosses.stars", new Map(WildbloodSettings.Bosses.BossStars) },
            { "bosses.aspects.enabled", new Map(WildbloodSettings.Bosses.Aspects) },
            { "loot.mode", new Map(WildbloodSettings.Loot.Mode) },
            { "loot.extra roll chance", new Map(WildbloodSettings.Loot.ExtraRollChance, t => string.Join(", ", SimpleYaml.List(t))) },
            { "loot.max extra rolls", new Map(WildbloodSettings.Loot.MaxExtraRolls) },
            { "loot.multiply trophies", new Map(WildbloodSettings.Loot.MultiplyTrophies) },
            { "loot.global multiplier", new Map(WildbloodSettings.Cheats.CreatureDropMultiplier) },
            { "loot.boss multiplier", new Map(WildbloodSettings.Cheats.BossDropMultiplier) },
        };

        /// <summary>Rule-file blocks that are not settings: where they go instead.</summary>
        private static readonly Dictionary<string, string> EcrRuleBlocks = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "lock to server", "dropped: Crucible always binds clients to the server's values" },
            { "respawning", "handed to Delve's converter" },
            { "breeding", "handed to Shepherd's converter" },
            { "mutations enabled", "imported with rules.yml (B02b)" },
            { "defaults", "imported with rules.yml (B02b)" },
            { "biomes", "imported with rules.yml (B02b)" },
            { "creatures", "imported with rules.yml (B02b)" },
            { "world tiers", "star and mutation boost lines imported with rules.yml (B02b)" },
            { "bosses", "star chances, star power and aspect tables imported with rules.yml (B02b)" },
        };

        public static ImportPlan Plan(ImportSource source, string? text)
        {
            var values = new List<ImportValue>();
            var notes = new List<ImportNote>();
            if (source == ImportSource.EliteCreaturesRebornRules) PlanRules(text, values, notes);
            else PlanConfig(source, text, values, notes);
            return new ImportPlan(source, values, notes);
        }

        private static void PlanConfig(ImportSource source, string? text, List<ImportValue> values, List<ImportNote> notes)
        {
            var (maps, skips) = source == ImportSource.EliteCreaturesRebornConfig ? (Ecr, EcrSkips)
                : source == ImportSource.CreatureManagerConfig ? (Cm, CmSkips) : (Sylb, new Dictionary<string, string>());
            var palette = new List<string>();
            foreach (var entry in SourceConfig.Parse(text))
            {
                var from = entry.Section + "." + entry.Key;
                if (source == ImportSource.EliteCreaturesRebornConfig && entry.Section == EcrPalette && entry.Key.EndsWith(" star colour", StringComparison.Ordinal))
                {
                    palette.Add(entry.Key.Substring(0, entry.Key.Length - " star colour".Length) + "=" + entry.Value);
                    continue;
                }
                if (skips.TryGetValue(from, out var why))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Skipped, from, why));
                    continue;
                }
                if (!maps.TryGetValue(from, out var map))
                {
                    notes.Add(new ImportNote(ImportNoteKind.Unknown, from, "not a Wildblood setting"));
                    continue;
                }
                Add(map, entry.Value, from, values, notes);
            }
            if (palette.Count > 0)
                Add(new Map(WildbloodSettings.Display.Palette), string.Join(", ", palette), EcrPalette, values, notes);
        }

        private static void PlanRules(string? text, List<ImportValue> values, List<ImportNote> notes)
        {
            var yaml = SimpleYaml.Read(text);
            foreach (var pair in yaml)
            {
                if (EcrRules.TryGetValue(pair.Key, out var map))
                {
                    Add(map, pair.Value, pair.Key, values, notes);
                    if ((pair.Key == "loot.global multiplier" || pair.Key == "loot.boss multiplier") && !IsOne(pair.Value))
                        notes.Add(new ImportNote(ImportNoteKind.Skipped, pair.Key,
                            "the multiplier acts only while the 12 - Cheats.Drop Multipliers cheat is on; the import never switches a cheat on"));
                    continue;
                }
                var top = pair.Key.Split('.')[0];
                if (!EcrRuleBlocks.ContainsKey(top)) notes.Add(new ImportNote(ImportNoteKind.Unknown, pair.Key, "not a Wildblood setting"));
            }
            var tops = new HashSet<string>(yaml.Keys.Select(k => k.Split('.')[0]), StringComparer.Ordinal);
            foreach (var line in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var t = line.TrimEnd();
                if (t.Length > 0 && t[0] != ' ' && t[0] != '#' && t.EndsWith(":", StringComparison.Ordinal)) tops.Add(t.Substring(0, t.Length - 1).Trim());
            }
            foreach (var block in EcrRuleBlocks.Where(b => tops.Contains(b.Key)))
                notes.Add(new ImportNote(ImportNoteKind.Skipped, block.Key, block.Value));
        }

        private static void Add(Map map, string text, string from, List<ImportValue> values, List<ImportNote> notes)
        {
            if (values.Any(v => v.SettingId == map.Target.Id))
            {
                notes.Add(new ImportNote(ImportNoteKind.Skipped, from, "merged into " + map.Target.Id + " (first value kept)"));
                return;
            }
            values.Add(new ImportValue(map.Target.Id, Convert(map, text, from, notes), from));
        }

        private static object Convert(Map map, string text, string from, List<ImportNote> notes)
        {
            var target = map.Target;
            object Fallback(string why)
            {
                notes.Add(new ImportNote(ImportNoteKind.Fallback, from, why + "; Wildblood default " + ImportPlan.Format(target.DefaultValue)));
                return target.DefaultValue;
            }
            object Checked(object value)
            {
                var normalized = target.NormalizeBoxed(value);
                if (!Equals(normalized, value))
                    notes.Add(new ImportNote(ImportNoteKind.Clamped, from, ImportPlan.Format(value) + " changed to " + ImportPlan.Format(normalized)));
                return normalized;
            }
            if (map.Convert != null)
            {
                var converted = map.Convert(text);
                return converted == null ? Fallback("unknown value \"" + text + "\"") : Checked(converted);
            }
            switch (target)
            {
                case WildbloodSetting<bool> _:
                    if (Eq(text, "true") || Eq(text, "On")) return true;
                    if (Eq(text, "false") || Eq(text, "Off")) return false;
                    return Fallback("unreadable \"" + text + "\"");
                case WildbloodSetting<float> _:
                    return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && !float.IsNaN(f) && !float.IsInfinity(f)
                        ? Checked(f) : Fallback("unreadable \"" + text + "\"");
                case WildbloodSetting<int> _:
                    return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? Checked(i) : Fallback("unreadable \"" + text + "\"");
                case WildbloodSetting<string> _:
                    return Checked(text);
                default:
                    if (target.ValueType.IsEnum && target.Choices.Any(c => Eq(c, text.Replace(" ", ""))))
                        return Enum.Parse(target.ValueType, text.Replace(" ", ""), true);
                    return Fallback("unknown value \"" + text + "\"");
            }
        }

        private static object? LevelSystem(string text) =>
            Eq(text, "Full") ? StarMode.Wildblood : Eq(text, "ExceptLevel") ? StarMode.KeepExisting : Eq(text, "Off") ? (object)StarMode.Off : null;

        private static bool IsOne(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) && f == 1f;

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
    }
}
