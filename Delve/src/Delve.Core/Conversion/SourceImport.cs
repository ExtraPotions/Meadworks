// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: converter plans (design 10). Source setting names come from each tool's own config file (names only);
// source files are never changed.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ExtraPotions.Delve.Config;
using ExtraPotions.Delve.Data;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Conversion
{
    public enum ImportSource { MaxDungeonRooms, MoreDungeonResets, RestockRegen, ValheimAdminTool, EliteCreaturesReborn }

    public sealed class ImportValue
    {
        internal ImportValue(string settingId, object value, string from)
        {
            SettingId = settingId;
            Value = value;
            From = from;
        }

        public string SettingId { get; }

        /// <summary>Typed as the setting (bool, int, float, string, <see cref="ClockKind"/>) and normalized to its range.</summary>
        public object Value { get; }

        /// <summary>The source key it came from.</summary>
        public string From { get; }
    }

    /// <summary>What an import would write: settings, Delve.Dungeons.yml modes, Delve.Sites.yml radii, C05 text overrides, notes.</summary>
    public sealed class ImportPlan
    {
        internal ImportPlan(ImportSource source) => Source = source;

        public ImportSource Source { get; }
        internal List<ImportValue> ValueList { get; } = new List<ImportValue>();
        internal Dictionary<string, DungeonMode> ModeMap { get; } = new Dictionary<string, DungeonMode>(StringComparer.Ordinal);
        internal Dictionary<string, (float, float)> SiteMap { get; } = new Dictionary<string, (float, float)>(StringComparer.Ordinal);
        internal Dictionary<string, string> TextMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
        internal List<string> NoteList { get; } = new List<string>();

        public IReadOnlyList<ImportValue> Values => ValueList;
        public IReadOnlyDictionary<string, DungeonMode> DungeonModes => ModeMap;

        /// <summary>Per generator: (entrance radius, inside radius) for Delve.Sites.yml.</summary>
        public IReadOnlyDictionary<string, (float Entrance, float Inside)> Sites => SiteMap.ToDictionary(p => p.Key, p => (p.Value.Item1, p.Value.Item2));

        /// <summary>C05 override texts by Delve catalog key, <c>{days}</c> already turned into <c>{0}</c>.</summary>
        public IReadOnlyDictionary<string, string> Texts => TextMap;

        public IReadOnlyList<string> Notes => NoteList;

        /// <summary>The preview text (written next to the config by the plugin).</summary>
        public string Report()
        {
            var inv = CultureInfo.InvariantCulture;
            var text = new StringBuilder("Delve import preview: " + Source + "\n\nSettings:\n");
            foreach (var v in ValueList) text.Append("  ").Append(v.SettingId).Append(" = ").Append(Convert.ToString(v.Value, inv)).Append("   (from ").Append(v.From).Append(")\n");
            if (ModeMap.Count > 0)
            {
                text.Append("\n").Append(DungeonsFile.FileName).Append(" modes:\n");
                foreach (var m in ModeMap.OrderBy(p => p.Key, StringComparer.Ordinal)) text.Append("  ").Append(m.Key).Append(": ").Append(m.Value).Append('\n');
            }
            if (SiteMap.Count > 0)
            {
                text.Append("\nDelve.Sites.yml radii (entrance, inside):\n");
                foreach (var s in SiteMap.OrderBy(p => p.Key, StringComparer.Ordinal))
                    text.Append("  ").Append(s.Key).Append(": ").Append(s.Value.Item1.ToString(inv)).Append(", ").Append(s.Value.Item2.ToString(inv)).Append('\n');
            }
            if (TextMap.Count > 0)
            {
                text.Append("\nText overrides (C05):\n");
                foreach (var t in TextMap.OrderBy(p => p.Key, StringComparer.Ordinal)) text.Append("  ").Append(t.Key).Append(": ").Append(t.Value).Append('\n');
            }
            if (NoteList.Count > 0)
            {
                text.Append("\nNotes:\n");
                foreach (var n in NoteList) text.Append("  - ").Append(n).Append('\n');
            }
            return text.ToString();
        }

        internal void Add(string settingId, object? raw, string from)
        {
            if (raw == null) return;
            var setting = DelveSettings.Find(settingId) ?? throw new InvalidOperationException("Converter names an unknown setting " + settingId + ".");
            object value = raw;
            if (setting.ValueType == typeof(float)) value = Convert.ToSingle(raw, CultureInfo.InvariantCulture);
            else if (setting.ValueType == typeof(int)) value = Convert.ToInt32(raw, CultureInfo.InvariantCulture);
            ValueList.RemoveAll(v => v.SettingId == setting.Id);
            ValueList.Add(new ImportValue(setting.Id, setting.NormalizeBoxed(value), from));
        }
    }

    public static class SourceImport
    {
        public static string FileName(ImportSource source)
        {
            switch (source)
            {
                case ImportSource.MaxDungeonRooms: return "digitalroot.mods.maxdungeonrooms.cfg";
                case ImportSource.MoreDungeonResets: return "com.loathe.MoreDungeonResets.cfg";
                case ImportSource.RestockRegen: return "liekos47.restockregen.cfg";
                case ImportSource.ValheimAdminTool: return "com.icecub.valheimadmintool.cfg";
                case ImportSource.EliteCreaturesReborn: return "creature_rules.yml";
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
        }

        public static ImportPlan Plan(ImportSource source, string? text)
        {
            var plan = new ImportPlan(source);
            switch (source)
            {
                case ImportSource.MaxDungeonRooms: MaxDungeonRooms(plan, CfgFile.Parse(text)); break;
                case ImportSource.MoreDungeonResets: MoreDungeonResets(plan, CfgFile.Parse(text)); break;
                case ImportSource.RestockRegen: RestockRegen(plan, CfgFile.Parse(text)); break;
                case ImportSource.ValheimAdminTool: AdminTool(plan, CfgFile.Parse(text)); break;
                case ImportSource.EliteCreaturesReborn: CreatureRules(plan, text); break;
                default: throw new ArgumentException("Unknown source.", nameof(source));
            }
            return plan;
        }

        /// <summary>The scalar entries of the top-level <c>respawning:</c> block of Elite Creatures Reborn's rules file.</summary>
        public static IReadOnlyDictionary<string, string> RespawningBlock(string? creatureRules)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (creatureRules == null) return result;
            var sections = MiniYaml.Read(creatureRules, new List<string>());
            if (sections.TryGetValue("respawning", out var entries))
                foreach (var e in entries)
                    if (e.Scalar != null) result[e.Name] = e.Scalar;
            return result;
        }

        // Max_Dungeon_Rooms (behavior reference): its four sections map one to one onto Delve's Rooms section.
        static void MaxDungeonRooms(ImportPlan plan, CfgFile cfg)
        {
            plan.Add("Rooms.Room Counts Enabled", true, "(installed)");
            plan.Add("Rooms.Min Rooms", cfg.Int("1. General", "Min Rooms"), "Min Rooms");
            plan.Add("Rooms.Max Rooms", cfg.Int("1. General", "Max Rooms"), "Max Rooms");
            foreach (var (section, name) in new[] { ("2. Forest Crypt Overrides", "Forest Crypt"), ("3. Sunken Crypt Overrides", "Sunken Crypt"), ("4. Cave Overrides", "Cave") })
            {
                plan.Add("Rooms." + name + " Override", cfg.Bool(section, "Enable " + name + " Override"), "Enable " + name + " Override");
                plan.Add("Rooms." + name + " Min Rooms", cfg.Int(section, name + " Min Rooms"), name + " Min Rooms");
                plan.Add("Rooms." + name + " Max Rooms", cfg.Int(section, name + " Max Rooms"), name + " Max Rooms");
            }
            plan.NoteList.Add("NexusID is not imported (Delve has no update check).");
        }

        static readonly (string Key, string? Generator)[] NamedResets =
        {
            ("Reset Sunken Crypts", "DG_SunkenCrypt"), ("Reset Troll Caves", null), ("Reset Troll Caves 02", null),
            ("Reset Burial Chambers", "DG_ForestCrypt"), ("Reset Frost Caves", "DG_Cave"), ("Reset Infested Mines", "DG_DvergrTown"),
            ("Reset Queen Citadel", "DG_DvergrBoss"), ("Reset Smouldering Tombs", "DG_Hildir_ForestCrypt"),
            ("Reset Howling Caverns", "DG_Hildir_Cave"), ("Reset Sealed Tower", "DG_Hildir_PlainsFortress"),
        };

        /// <summary>Generators that build a surface camp or village, not an interior: a full reset has nothing to wipe there.</summary>
        public static readonly IReadOnlyList<string> SurfaceGenerators = Array.AsReadOnly(new[] { "DG_GoblinCamp", "DG_MeadowsVillage", "DG_MeadowsFarm", "DG_NorthVillage" });

        // MoreDungeonResets (behavior reference, no license): delay in in-game days; each type it resets becomes Full.
        static void MoreDungeonResets(ImportPlan plan, CfgFile cfg)
        {
            bool auto = cfg.Bool("1 - Global", "Enable Auto Reset") ?? true;
            plan.Add("Dungeons.Full Reset Clock", ClockKind.InGameDays, "Reset Interval (Days)");
            plan.Add("Dungeons.Full Reset Delay", cfg.Int("1 - Global", "Reset Interval (Days)"), "Reset Interval (Days)");
            if (cfg.Bool("1 - Global", "Enable Debug Logging") == true)
                plan.NoteList.Add("Enable Debug Logging is not imported: Meadworks logging (C06) replaces it.");
            if (!auto)
            {
                plan.NoteList.Add("Enable Auto Reset is off: no dungeon type is set to Full.");
                return;
            }
            // A named switch (sections 2 and 3) set to false wins over the generator's own entry in section 4: the Sealed
            // Tower is off in Solo while DG_Hildir_PlainsFortress is listed on.
            var namedOff = new HashSet<string>(cfg.Entries.Where(e => e.Value.Equals("false", StringComparison.OrdinalIgnoreCase))
                .Select(e => NamedResets.FirstOrDefault(n => n.Key == e.Key).Generator).Where(g => g != null).Select(g => g!), StringComparer.Ordinal);
            foreach (var (section, key, value) in cfg.Entries)
            {
                if (!section.StartsWith("2 -", StringComparison.Ordinal) && !section.StartsWith("3 -", StringComparison.Ordinal) && !section.StartsWith("4 -", StringComparison.Ordinal)) continue;
                if (!bool.TryParse(value, out var on) || !on) continue;
                string? generator;
                var named = NamedResets.FirstOrDefault(n => n.Key == key);
                if (named.Key != null) generator = named.Generator;
                else if (key.StartsWith("Reset DG_", StringComparison.Ordinal)) generator = key.Substring("Reset ".Length);
                else
                {
                    plan.NoteList.Add("'" + key + "' is not a known MoreDungeonResets switch; skipped.");
                    continue;
                }
                if (generator == null)
                {
                    plan.NoteList.Add("'" + key + "': Troll Caves are not generated dungeons; Delve has no full reset for them.");
                    continue;
                }
                if (namedOff.Contains(generator))
                {
                    plan.NoteList.Add("'" + key + "' is on but the named switch for " + generator + " is off; the named switch wins.");
                    continue;
                }
                if (SurfaceGenerators.Contains(generator))
                {
                    plan.NoteList.Add("'" + key + "': " + generator + " is a surface camp without an interior; it keeps its default mode.");
                    continue;
                }
                plan.ModeMap[generator] = DungeonMode.Full;
            }
            plan.NoteList.Add("Full resets start their clocks at the first evaluation after the import (MoreDungeonResets' own clocks are not readable).");
        }

        static readonly (string Kind, string Prefix, string Section, string Key)[] RegrowKinds =
        {
            ("Mud Piles", "MudPile", "Dungeon Resources", "mudpiles"), ("Frost Cave Crystals", "Crystal", "Dungeon Resources", "crystals"),
            ("Black Cores", "BlackCore", "Dungeon Resources", "blackcores"), ("Morkhalla", "Morkhalla", "Dungeon Resources", "morkhalla"),
            ("Hole", "Hole", "Dungeon Resources", "hole"),
        };

        static readonly (string Switch, string Kind)[] RegrowSwitches =
        {
            ("MudPiles", "Mud Piles"), ("FrostCaveCrystals", "Frost Cave Crystals"), ("BlackCores", "Black Cores"), ("Morkhalla", "Morkhalla"), ("TheHole", "Hole"),
        };

        static readonly (string Entrance, string Inside, string Generator)[] SiteKeys =
        {
            ("EntranceRadius", "CryptRadius", "DG_SunkenCrypt"), ("CaveEntranceRadius", "CaveRadius", "DG_Cave"),
            ("MineEntranceRadius", "MineRadius", "DG_DvergrTown"), ("MorkhallaEntranceRadius", "MorkhallaRadius", "DG_MorkHalla"),
            ("HoleEntranceRadius", "HoleRadius", "DG_Hole"), ("CitadelEntranceRadius", "CitadelRadius", "DG_DvergrBoss"),
        };

        static readonly (string Source, string Key)[] TextKeys =
        {
            ("NotifyText", "chest_refill"), ("NotifyLeftoversText", "chest_refill_leftovers"), ("NotifyDueText", "chest_due"),
            ("HistoryText", "history_many"), ("HistoryTextOnce", "history_once"),
            ("MudPileNotifyText", "mudpiles_inside"), ("MudPileEntranceText", "mudpiles_entrance"), ("MudPileDueText", "mudpiles_due"),
            ("CrystalNotifyText", "crystals_inside"), ("CrystalEntranceText", "crystals_entrance"), ("CrystalDueText", "crystals_due"),
            ("BlackCoreNotifyText", "blackcores_inside"), ("BlackCoreEntranceText", "blackcores_entrance"), ("BlackCoreDueText", "blackcores_due"),
            ("MorkhallaNotifyText", "morkhalla_inside"), ("MorkhallaEntranceText", "morkhalla_entrance"), ("MorkhallaDueText", "morkhalla_due"),
            ("HoleNotifyText", "hole_inside"), ("HoleEntranceText", "hole_entrance"), ("HoleDueText", "hole_due"),
            ("CitadelNotifyText", "queen_inside"), ("CitadelEntranceText", "queen_entrance"), ("CitadelDueText", "queen_due"),
            ("AncientArmorNotifyText", "biome_mistlands"), ("MountainsNotifyText", "biome_mountain"),
            ("AshlandsNotifyText", "biome_ashlands"), ("DeepNorthNotifyText", "biome_deepnorth"),
        };

        /// <summary>The C05 keys the converter can write overrides for.</summary>
        public static IEnumerable<string> TextOverrideKeys => TextKeys.Select(t => t.Key);

        // RestockRegen (MIT): every section by name; RestockRegen's key names are unique across its sections.
        static void RestockRegen(ImportPlan plan, CfgFile cfg)
        {
            string? T(string key) { var s = cfg.SectionOf(key); return s == null ? null : cfg.Text(s, key); }
            bool? B(string key) => bool.TryParse(T(key), out var b) ? b : (bool?)null;
            int? I(string key) => int.TryParse(T(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int?)null;
            float? F(string key) => float.TryParse(T(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : (float?)null;

            plan.Add("General.Enabled", B("Enabled"), "Enabled");
            plan.Add("General.Check Interval Seconds", I("VisitCheckSeconds"), "VisitCheckSeconds");
            plan.Add("Chests.Chest Delay", I("Days"), "Days");
            plan.Add("Chests.Chest Dry Run", B("DryRun"), "DryRun");
            plan.Add("Chests.Reset Opened", B("ResetOpened"), "ResetOpened");
            plan.Add("Chests.Restock Loaded", B("RestockLoaded"), "RestockLoaded");
            if (T("Exclude") != null) plan.Add("Chests.Exclude", T("Exclude"), "Exclude");
            plan.Add("Chests.History", B("History"), "History");
            plan.Add("Notices.Notify", B("Notify"), "Notify");
            foreach (var (sw, kind) in RegrowSwitches) plan.Add("Dungeon Resources." + kind, B(sw), sw);
            foreach (var (kind, prefix, section, _) in RegrowKinds)
            {
                plan.Add(section + "." + kind + " Delay", I(prefix + "Days"), prefix + "Days");
                plan.Add(section + "." + kind + " Dry Run", B(prefix + "DryRun"), prefix + "DryRun");
            }
            plan.Add("Bosses.Queen Return", B("Citadel"), "Citadel");
            plan.Add("Bosses.Queen Delay", I("CitadelDays"), "CitadelDays");
            plan.Add("Bosses.Queen Dry Run", B("CitadelDryRun"), "CitadelDryRun");
            plan.Add("Bosses.Close Sealed Door", B("CitadelCloseDoor"), "CitadelCloseDoor");
            foreach (var kind in new[] { "AncientArmor", "Obsidian", "Flametal", "Ice" })
            {
                var name = kind == "AncientArmor" ? "Ancient Armor" : kind;
                plan.Add("World Objects." + name, B(kind), kind);
                plan.Add("World Objects." + name + " Delay", I(kind + "Days"), kind + "Days");
                plan.Add("World Objects." + name + " Dry Run", B(kind + "DryRun"), kind + "DryRun");
                plan.Add("World Objects." + name + " Visit Radius", F(kind + "VisitRadius"), kind + "VisitRadius");
                plan.Add("World Objects." + name + " Build Clearance", F(kind + "BuildClearance"), kind + "BuildClearance");
            }
            var cooldowns = new[] { "AncientArmorNotifyCooldown", "MountainsNotifyCooldown", "AshlandsNotifyCooldown", "DeepNorthNotifyCooldown" }
                .Select(k => F(k)).Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (cooldowns.Count > 0)
            {
                plan.Add("Notices.Biome Notice Cooldown Minutes", cooldowns.Min(), "NotifyCooldown");
                if (cooldowns.Distinct().Count() > 1) plan.NoteList.Add("The four biome cooldowns differ; Delve has one and takes the shortest.");
            }
            foreach (var (entrance, inside, generator) in SiteKeys)
            {
                var e = F(entrance);
                var i = F(inside);
                if (e.HasValue && i.HasValue) plan.SiteMap[generator] = (e.Value, i.Value);
            }
            foreach (var (source, key) in TextKeys)
            {
                var text = T(source);
                if (!string.IsNullOrEmpty(text)) plan.TextMap[key] = text!.Replace("{days}", "{0}");
            }
            if (B("Verbose") == true) plan.NoteList.Add("Verbose is not imported: Meadworks logging (C06) replaces it.");
            if (cfg.Entries.Any(e => e.Key.EndsWith("RegenNow", StringComparison.Ordinal)))
                plan.NoteList.Add("RegenNow switches are one-shot commands and are not imported; use Delve's regenerate-now commands.");
            plan.NoteList.Add("RestockRegen's world data (chest clocks, history, remembered spots, visits) is copied at the first world load where Delve has none; RestockRegen's keys stay in place.");
        }

        // Valheim_Admin_Tool (behavior reference, no license): its automatic resets run on real hours.
        static void AdminTool(ImportPlan plan, CfgFile cfg)
        {
            const string section = "Automatic resets";
            if (cfg.Bool(section, "Reset dungeons automatically") == true)
            {
                plan.Add("Dungeons.Full Reset Clock", ClockKind.RealHours, "Reset dungeons automatically");
                plan.Add("Dungeons.Full Reset Delay", cfg.Float(section, "Dungeon reset interval hours"), "Dungeon reset interval hours");
                plan.NoteList.Add("Valheim_Admin_Tool reset every dungeon; set the types to reset to Full in " + DungeonsFile.FileName + ".");
            }
            else plan.NoteList.Add("Automatic dungeon resets are off in Valheim_Admin_Tool: nothing imported for full resets.");
            if (cfg.Bool(section, "Reset resources automatically") == true)
            {
                var hours = cfg.Float(section, "Resource reset interval hours");
                foreach (var k in DelveSettings.Resources.Concat(DelveSettings.WorldObjects))
                {
                    plan.Add(k.Clock.Id, ClockKind.RealHours, "Reset resources automatically");
                    plan.Add(k.Delay.Id, hours, "Resource reset interval hours");
                }
                plan.NoteList.Add("Valheim_Admin_Tool's resource reset maps to Delve's dungeon resource and world object kinds on real hours.");
            }
            else plan.NoteList.Add("Automatic resource resets are off in Valheim_Admin_Tool: nothing imported for resources.");
        }

        // Elite Creatures Reborn (GPL-3.0-or-later): the respawning block that Wildblood hands to Delve.
        static void CreatureRules(ImportPlan plan, string? text)
        {
            var block = RespawningBlock(text);
            if (block.Count == 0)
            {
                plan.NoteList.Add("No respawning block found in creature_rules.yml.");
                return;
            }
            bool? B(string key) => block.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : (bool?)null;
            float? F(string key) => block.TryGetValue(key, out var v) && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : (float?)null;
            plan.Add("Creature Respawn.Camps", B("camps"), "camps");
            plan.Add("Creature Respawn.Camps Delay", F("camp days"), "camp days");
            plan.Add("Creature Respawn.Dungeon Spawners", B("dungeons"), "dungeons");
            plan.Add("Creature Respawn.Dungeon Spawners Delay", F("dungeon days"), "dungeon days");
            if (B("dungeon loot") == true)
            {
                plan.Add("Chests.Chests", true, "dungeon loot");
                plan.Add("Chests.Chest Delay", F("dungeon loot days"), "dungeon loot days");
                plan.Add("Chests.Reset Opened", false, "dungeon loot");
                plan.NoteList.Add("Elite Creatures Reborn refilled only emptied dungeon chests: Reset Opened is turned off.");
            }
            else plan.NoteList.Add("Dungeon loot is off in Elite Creatures Reborn: Delve's chest settings are left as they are.");
        }
    }
}
