// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
// Existing Meadworks source readers and native target bounds own parsing and final validation.
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Shepherd.Conversion
{
    public enum ImportSource { AutoFeedRedux, GrindstoneSkills, EliteCreaturesReborn }
    public sealed class ImportPlan
    {
        public ImportSource Source { get; }
        public Dictionary<string,string> Values { get; } = new Dictionary<string,string>(StringComparer.Ordinal);
        public List<string> Notes { get; } = new List<string>();
        internal ImportPlan(ImportSource source) { Source = source; }
        public string Report() => "Source: " + Source + "\n" + string.Join("\n", Values.Select(v => "Set " + v.Key + " = " + v.Value).Concat(Notes));
    }
    public static class SourceImport
    {
        public static string FileName(ImportSource source) => source == ImportSource.AutoFeedRedux ? "vapok.mods.AutoFeedRedux.cfg"
            : source == ImportSource.GrindstoneSkills ? "com.GrindstoneSkills.cfg" : "creature_rules.yml";
        static readonly Dictionary<string,string> mappings = BuildMappings();
        static Dictionary<string,string> BuildMappings()
        {
            var map = new Dictionary<string,string>(StringComparer.Ordinal);
            void Set(string section, string key, string targetSection, string targetKey) => map[section + "." + key] = targetSection + "." + targetKey;
            void Same(string section, string target, string names) { foreach (var name in names.Split('|')) Set(section, name, target, name); }
            Set("Synced Settings", "Enable Auto Feeder", "2 - Feeding", "Container Feeding");
            Set("Synced Settings", "Feed Range in Meters", "2 - Feeding", "Container Range");
            Set("Synced Settings", "Require Move to Feed", "2 - Feeding", "Require Move To Feed");
            Set("Synced Settings", "Disallow Animal", "2 - Feeding", "Disallow Animals");
            Same("Synced Settings", "2 - Feeding", "Move Proximity|Disallow Feed|Protect Feed Containers");
            Set("23 - Husbandry", "Husbandry Enabled", "1 - General", "Husbandry Enabled");
            Same("23 - Husbandry", "4 - Husbandry", "Keeper Range|Animal Lore Level|Show Callouts");
            Same("24 - Taming", "5 - Taming", "Taming Speed At 100|Fed Duration At 100|Taming Levels|Calm Level|Calm Break Time");
            Same("25 - Breeding", "6 - Breeding", "Breeding Speed At 100|Growth Speed At 100|Better Offspring At 100|Max Offspring Level|Twins At 100|Content Duration|Content Breeding Bonus");
            Set("25 - Breeding", "Herd Size At 100", "7 - Herd", "Herd Size At 100");
            Same("26 - Animal Yield", "8 - Yield", "Butcher Yield At 100|Produce Chance At 100|Produce Interval|Extra Honey At 100");
            Same("27 - Companions", "9 - Companions", "Pack Damage At 100|Pack Toughness At 100");
            Set("27 - Companions", "Feeder Level", "3 - Troughs", "Build Level");
            Set("27 - Companions", "Feeder Range", "2 - Feeding", "Trough Range");
            Set("27 - Companions", "Feeder Recipe", "3 - Troughs", "Feed Barrel Recipe");
            Same("28 - Husbandry Experience", "10 - Experience", "Experience Multiplier|Taming Experience|Tamed Experience|Discovery Multiplier|Feeding Experience|Birth Experience|Petting Experience|Butchering Experience|Honey Experience");
            return map;
        }
        public static ImportPlan Plan(ImportSource source, string text)
        {
            var plan = new ImportPlan(source);
            if (source == ImportSource.EliteCreaturesReborn)
            {
                foreach (var entry in SimpleYaml.Read(text))
                {
                    if (entry.Key == "breeding.enabled")
                    {
                        if (!bool.TryParse(entry.Value, out var enabled)) throw new ArgumentException("Invalid breeding.enabled.");
                        if (enabled) plan.Values["6 - Breeding.Star Inheritance"] = "BestParentUniform";
                        else plan.Notes.Add("Skip breeding.enabled=false: source inheritance is disabled.");
                    }
                    else if (entry.Key == "breeding.mutation chance") plan.Values["6 - Breeding.Mutation Chance"] = entry.Value;
                    else plan.Notes.Add("Skip " + entry.Key + ": outside Shepherd mappings.");
                }
                return plan;
            }
            foreach (var entry in SourceConfig.Parse(text))
            {
                string key = entry.Section + "." + entry.Key;
                bool ownSource = source == ImportSource.AutoFeedRedux ? entry.Section == "Synced Settings" : entry.Section != "Synced Settings";
                if (ownSource && mappings.TryGetValue(key, out var target)) plan.Values[target] = entry.Value;
                else plan.Notes.Add("Skip " + key + ": outside Shepherd mappings.");
            }
            return plan;
        }
    }
}
