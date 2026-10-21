// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the `aspects:` block under `bosses:` onto the aspect defaults, in the same shape as the other overlays: only
    /// the keys the block names change, an aspect name the mod does not know is reported against its line, and a value
    /// that will not parse keeps its default rather than throwing.
    /// </summary>
    internal static class AspectOverlay
    {
        public static void Apply(AspectRules rules, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            rules.Enabled = YamlRead.Bool(block, Fields.Enabled, rules.Enabled, errors);
            ReadShift(rules, block, errors, warnings);
            ReadWeights(rules.Chances, block, Fields.Chances, errors);
            ReadWeights(rules.Loot, block, Fields.Loot, errors);
            ReadPower(rules, block, errors, warnings);
            PerBossOverlay.Apply(rules, YamlRead.Child(block, Fields.PerBoss), errors);
        }

        /// <summary>
        /// The altar shift interval: `shift seconds`, or an older file's `shift hours` when it names only that, so a file
        /// written before the seconds came keeps its interval. When both are named, `shift seconds` wins and the hours
        /// only warn.
        /// </summary>
        private static void ReadShift(AspectRules rules, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            YamlNode? hours = YamlRead.Child(block, Fields.ShiftHours);
            if (YamlRead.Child(block, Fields.ShiftSeconds) != null)
            {
                rules.ShiftSeconds = NonNegative(block, Fields.ShiftSeconds, rules.ShiftSeconds, errors);
                rules.ShiftHours = null;
                if (hours != null)
                {
                    YamlRead.AddError(warnings, hours, $"'{Fields.ShiftHours}' is ignored: '{Fields.ShiftSeconds}' is set");
                }
            }
            else if (hours != null)
            {
                float value = NonNegative(block, Fields.ShiftHours, -1f, errors); // -1: it did not parse, already reported
                rules.ShiftHours = value >= 0f ? value : rules.ShiftHours;
            }
        }

        private static float NonNegative(YamlMappingNode block, string key, float fallback, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlRead.TryFloat(node, out float value) && value >= 0f)
            {
                return value;
            }
            YamlRead.AddError(errors, node, $"'{key}' should be a number, 0 or more");
            return fallback;
        }

        /// <summary>A block of `aspect: number` lines; each named aspect replaces its default, the rest stay.</summary>
        private static void ReadWeights(Dictionary<Aspect, float> into, YamlMappingNode block, string key, List<string> errors)
        {
            if (!(YamlRead.Child(block, key) is YamlNode node) || !(YamlRead.Map(node, errors, $"'{key}'") is YamlMappingNode map))
            {
                return;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                Aspect? aspect = Name(pair.Key, errors);
                if (aspect != null && YamlRead.TryFloat(pair.Value, out float value) && value >= 0f)
                {
                    into[aspect.Value] = value;
                }
                else if (aspect != null)
                {
                    YamlRead.AddError(errors, pair.Value, $"'{key}' for {AspectCatalog.Key(aspect.Value)} should be a number, 0 or more");
                }
            }
        }

        private static void ReadPower(AspectRules rules, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            if (!(YamlRead.Child(block, Fields.Power) is YamlNode node) || !(YamlRead.Map(node, errors, "'power'") is YamlMappingNode map))
            {
                return;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                Aspect? aspect = Name(pair.Key, errors);
                if (aspect != null && YamlRead.Map(pair.Value, errors, $"power for {aspect}") is YamlMappingNode fields)
                {
                    ReadFields(rules, aspect.Value, fields, errors, warnings);
                }
            }
        }

        /// <summary>One aspect's fields: a number, or a list like Phantom's `split at`. A retired field only warns.</summary>
        private static void ReadFields(AspectRules rules, Aspect aspect, YamlMappingNode fields, List<string> errors,
            List<string> warnings)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in fields.Children)
            {
                string field = (pair.Key as YamlScalarNode)?.Value ?? "";
                if (Retired(aspect, field) is string note)
                {
                    YamlRead.AddError(warnings, pair.Key, note);
                }
                else if (pair.Value is YamlSequenceNode)
                {
                    ReadList(rules, aspect, field, pair.Value, errors);
                }
                else
                {
                    ReadNumber(rules, aspect, field, pair.Value, errors);
                }
            }
        }

        private static void ReadNumber(AspectRules rules, Aspect aspect, string field, YamlNode node, List<string> errors)
        {
            if (YamlRead.TryFloat(node, out float value))
            {
                Into(rules.Power, aspect)[field] = value;
            }
            else
            {
                YamlRead.AddError(errors, node, $"{aspect} '{field}' is not a number");
            }
        }

        private static void ReadList(AspectRules rules, Aspect aspect, string field, YamlNode node, List<string> errors)
        {
            float[]? values = YamlRead.Floats(node, errors, $"{aspect} '{field}'");
            Into(rules.Lists, aspect)[field] = values ?? new float[0]; // an empty list turns the field off
        }

        private static Dictionary<string, T> Into<T>(Dictionary<Aspect, Dictionary<string, T>> table, Aspect aspect)
        {
            if (!table.TryGetValue(aspect, out Dictionary<string, T> into))
            {
                table[aspect] = into = new Dictionary<string, T>();
            }
            return into;
        }

        /// <summary>
        /// A field an older rule file still carries but that no longer does anything - Phantom's fixed copy count and flat
        /// health, from before its copies came per player at each split. A warning, not an error: an error would reject
        /// the whole file, and the line is merely out of date.
        /// </summary>
        private static string? Retired(Aspect aspect, string field)
        {
            if (aspect == Aspect.Phantom && field == Fields.Copies)
            {
                return $"Phantom '{field}' is no longer used: the boss splits off '{Fields.PerPlayer}' copies for each "
                    + $"player online at every '{Fields.SplitAt}' mark";
            }
            if (aspect == Aspect.Phantom && field == Fields.Health)
            {
                return $"Phantom '{field}' is no longer used: each copy has '{Fields.HealthPerTier}' health for each "
                    + "world tier (tier 0 counts as 1)";
            }
            return null;
        }

        /// <summary>The aspect a key names, `none` included; an unknown word is reported and skipped.</summary>
        public static Aspect? Name(YamlNode key, List<string> errors)
        {
            string name = (key as YamlScalarNode)?.Value ?? "";
            Aspect? aspect = AspectCatalog.FromName(name);
            if (aspect == null)
            {
                YamlRead.AddError(errors, key, $"'{name}' is not a boss aspect - use none or an aspect's name");
            }
            return aspect;
        }
    }
}
