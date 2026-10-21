// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System;
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the rule file's `loot:` block and the loot keys of each `creatures:` entry, in the same shape as the other
    /// overlays: only the keys a block names change, every problem is recorded against its line, and a value that will
    /// not parse keeps its default rather than throwing.
    /// </summary>
    internal static class LootOverlay
    {
        public static void Apply(LootRules loot, YamlMappingNode block, List<string> errors)
        {
            Mode(loot, block, errors);
            float[]? chances = YamlRead.Child(block, Fields.ExtraRollChance) is YamlNode node
                ? YamlRead.Floats(node, errors, $"'{Fields.ExtraRollChance}'") : null;
            if (chances != null)
            {
                loot.ExtraRollChance = chances;
            }
            loot.MaxExtraRolls = Math.Max(0, YamlRead.Int(block, Fields.MaxExtraRolls, loot.MaxExtraRolls, errors));
            loot.GlobalMultiplier = Positive(block, Fields.GlobalMultiplier, loot.GlobalMultiplier, errors);
            loot.BossMultiplier = Positive(block, Fields.BossMultiplier, loot.BossMultiplier, errors);
            loot.MultiplyTrophies = YamlRead.Bool(block, Fields.MultiplyTrophies, loot.MultiplyTrophies, errors);
        }

        private static void Mode(LootRules loot, YamlMappingNode block, List<string> errors)
        {
            string? name = YamlRead.Scalar(block, Fields.Mode);
            if (name == null)
            {
                return;
            }
            if (Enum.TryParse(name, ignoreCase: true, out LootMode mode))
            {
                loot.Mode = mode;
            }
            else
            {
                YamlRead.AddError(errors, block, $"'{name}' is not a loot mode - use Vanilla, Scaled, Rolled, Amount or Curated");
            }
        }

        private static float Positive(YamlMappingNode block, string key, float fallback, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlRead.TryFloat(node, out float value) && value > 0f)
            {
                return value;
            }
            YamlRead.AddError(errors, node, $"'{key}' should be a number above zero");
            return fallback;
        }

        /// <summary>
        /// One `creatures:` entry's loot keys, read into the creature's rule. A key the entry names replaces what an
        /// earlier entry for the same creature set, and its drop rows are appended, so two entries merge (see
        /// <see cref="CreatureOverlay"/>); a key it leaves out, or `drops: []`, keeps the earlier value.
        /// </summary>
        public static void ReadRule(CreatureLootRule rule, YamlMappingNode block, List<string> errors)
        {
            if (YamlRead.Child(block, Fields.Drops) is YamlNode drops
                && YamlRead.Floats(drops, errors, $"'{Fields.Drops}'") is float[] line)
            {
                rule.Drops = line;
            }
            if (YamlRead.Child(block, Fields.MultiplyTrophies) != null)
            {
                rule.MultiplyTrophies = YamlRead.Bool(block, Fields.MultiplyTrophies, false, errors);
            }
            ReadRows(rule.Overrides, block, Fields.DropOverrides, errors);
            ReadRows(rule.Extras, block, Fields.ExtraDrops, errors);
        }

        private static void ReadRows(List<DropRule> into, YamlMappingNode block, string key, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return;
            }
            if (!(node is YamlSequenceNode seq))
            {
                YamlRead.AddError(errors, node, $"'{key}' should be a list of drop rows");
                return;
            }
            foreach (YamlNode item in seq.Children)
            {
                ReadRow(into, item, key, errors);
            }
        }

        private static void ReadRow(List<DropRule> into, YamlNode item, string key, List<string> errors)
        {
            if (!(YamlRead.Map(item, errors, $"a '{key}' row") is YamlMappingNode block))
            {
                return;
            }
            string? name = YamlRead.Scalar(block, Fields.Item);
            if (string.IsNullOrEmpty(name))
            {
                YamlRead.AddError(errors, block, $"a '{key}' row has no '{Fields.Item}' prefab name");
                return;
            }
            into.Add(FillRow(name!, block, errors));
        }

        private static DropRule FillRow(string item, YamlMappingNode block, List<string> errors)
        {
            DropRule row = new DropRule
            {
                Item = item,
                Chance = Percent(block, Fields.Chance, 100f, errors),
                Remove = YamlRead.Bool(block, Fields.Remove, false, errors),
                PerStar = YamlRead.Bool(block, Fields.PerStar, false, errors),
            };
            Amount(row, block, errors);
            return row;
        }

        private static void Amount(DropRule row, YamlMappingNode block, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, Fields.Amount);
            if (node == null)
            {
                return;
            }
            float[]? pair = YamlRead.Floats(node, errors, $"'{Fields.Amount}'");
            if (pair == null || pair.Length != 2 || pair[0] < 0 || pair[1] < pair[0])
            {
                YamlRead.AddError(errors, node, $"'{Fields.Amount}' should be [min, max] with 0 <= min <= max");
                return;
            }
            row.AmountMin = (int)pair[0];
            row.AmountMax = (int)pair[1];
        }

        private static float Percent(YamlMappingNode block, string key, float fallback, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return fallback;
            }
            if (YamlRead.TryFloat(node, out float value) && value >= 0f && value <= 100f)
            {
                return value;
            }
            YamlRead.AddError(errors, node, $"'{key}' should be a percentage from 0 to 100");
            return fallback;
        }
    }
}
