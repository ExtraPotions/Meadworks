// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Reads the file's top-level <c>world tiers</c> and <c>breeding</c> blocks onto their defaults; anything a block
    /// leaves out keeps its default, the same way every other block does. A rule file from before these blocks existed
    /// simply takes the defaults.
    /// </summary>
    internal static class WorldOverlay
    {
        public static void ApplyTiers(TierRules tiers, YamlMappingNode block, List<string> errors)
        {
            tiers.Enabled = YamlRead.Bool(block, Fields.Enabled, tiers.Enabled, errors);
            tiers.BossKeys = BossKeys(block, errors) ?? tiers.BossKeys;
            tiers.StarBoost = Boost(block, Fields.StarBoost, tiers.StarBoost, errors);
            tiers.MutationBoost = Boost(block, Fields.MutationBoost, tiers.MutationBoost, errors);
        }

        public static void ApplyBreeding(BreedingRules breeding, YamlMappingNode block, List<string> errors)
        {
            breeding.Enabled = YamlRead.Bool(block, Fields.Enabled, breeding.Enabled, errors);
            YamlNode? node = YamlRead.Child(block, Fields.MutationChance);
            if (node == null)
            {
                return;
            }
            if (YamlRead.TryFloat(node, out float value) && value >= 0f && value <= 100f)
            {
                breeding.MutationChance = value;
                return;
            }
            YamlRead.AddError(errors, node, $"'{Fields.MutationChance}' should be a percentage, 0 to 100");
        }

        // An empty list is allowed and means no boss counts, so the world stays at tier 0 with the feature still on.
        private static List<string>? BossKeys(YamlMappingNode block, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, Fields.BossKeys);
            if (node == null)
            {
                return null;
            }
            if (!(node is YamlSequenceNode seq))
            {
                YamlRead.AddError(errors, node, $"'{Fields.BossKeys}' should be a list like [defeated_eikthyr, ...]");
                return null;
            }
            List<string> keys = new List<string>();
            foreach (YamlNode item in seq.Children)
            {
                AddKey(keys, item, errors);
            }
            return keys;
        }

        // The game stores global keys in lower case, so the list does too; a key listed twice counts once.
        private static void AddKey(List<string> keys, YamlNode item, List<string> errors)
        {
            string key = ((item as YamlScalarNode)?.Value ?? "").Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                YamlRead.AddError(errors, item, $"'{Fields.BossKeys}' has an entry that is not a key name");
            }
            else if (!keys.Contains(key))
            {
                keys.Add(key);
            }
        }

        private static float[] Boost(YamlMappingNode block, string key, float[] current, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            if (node == null)
            {
                return current;
            }
            float[]? values = YamlRead.Floats(node, errors, $"'{key}'");
            if (values == null || values.Length == 0)
            {
                return current;
            }
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = NonNegative(values[i], node, key, errors);
            }
            return values;
        }

        private static float NonNegative(float value, YamlNode node, string key, List<string> errors)
        {
            if (value >= 0f)
            {
                return value;
            }
            YamlRead.AddError(errors, node, $"'{key}' has a negative entry - using 0");
            return 0f;
        }
    }
}
