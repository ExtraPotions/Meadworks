// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using System.Collections.Generic;
using ExtraPotions.Wildblood.Adopted.Traits;
using UnityEngine;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Wildblood.Adopted.Rules
{
    /// <summary>
    /// Lays one file block over a <see cref="BiomeRules"/>, changing only the keys the block names and leaving the rest
    /// as the defaults it was cloned from. This one routine serves the `defaults` block, every biome block and the
    /// mutation keys of every `creatures:` entry, so "a block overrides only what it names" is true by construction
    /// rather than by a second code path.
    /// </summary>
    internal static class RuleOverlay
    {
        /// <summary>The most star counts a distribution may list: index 0 (unstarred) through the 50-star ceiling.</summary>
        private const int MaxStarEntries = 51;

        private const string ChanceKey = "mutation chance";
        private const string ChancesKey = "mutation chances";
        private const string PowerKey = "mutation power";

        public static void Apply(BiomeRules rules, YamlMappingNode block, List<string> errors, List<string> warnings)
        {
            StarChances(rules, block, errors, warnings);
            LargeStarPower(rules, block, errors);
            Star(rules, block, errors);
            ApplyMutations(rules, block, errors);
        }

        /// <summary>Only the three mutation keys - all a `creatures:` entry may set - read exactly as a biome's are.</summary>
        public static void ApplyMutations(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            MutationChance(rules, block, errors);
            MutationChances(rules, block, errors);
            MutationPower(rules, block, errors);
        }

        /// <summary>True when the block names at least one of the mutation keys.</summary>
        public static bool NamesMutations(YamlMappingNode block)
        {
            return YamlRead.Child(block, ChanceKey) != null || YamlRead.Child(block, ChancesKey) != null
                || YamlRead.Child(block, PowerKey) != null;
        }

        private static void StarChances(BiomeRules rules, YamlMappingNode block, List<string> e, List<string> w)
        {
            float[]? values = ReadFloats(block, "star chances", e);
            if (values == null)
            {
                return;
            }
            rules.StarChances = Normalise(Truncate(values, w), w);
        }

        private static float[] Truncate(float[] values, List<string> warnings)
        {
            if (values.Length <= MaxStarEntries)
            {
                return values;
            }
            warnings.Add($"star chances lists {values.Length} entries, past the 50-star ceiling - truncating");
            float[] cut = new float[MaxStarEntries];
            System.Array.Copy(values, cut, MaxStarEntries);
            return cut;
        }

        private static void LargeStarPower(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, "large star power");
            if (node != null && YamlRead.TryFloat(node, out float value))
            {
                rules.LargeStarPower = value;
            }
            else if (node != null)
            {
                YamlRead.AddError(errors, node, "'large star power' is not a number");
            }
        }

        private static float[] Normalise(float[] values, List<string> warnings)
        {
            float sum = 0f;
            foreach (float v in values)
            {
                sum += Mathf.Max(0f, v);
            }
            if (sum <= 0f || Mathf.Abs(sum - 100f) < 0.01f)
            {
                return values;
            }
            warnings.Add($"star chances sum to {sum:0.##}, not 100 - normalizing");
            float[] scaled = new float[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                scaled[i] = Mathf.Max(0f, values[i]) / sum * 100f;
            }
            return scaled;
        }

        private static void Star(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            if (!(YamlRead.Child(block, "star power") is YamlMappingNode map))
            {
                return;
            }
            StarPower star = rules.Star;
            star.Growth = Line(map, Fields.Growth, star.Growth, errors);
            star.Hp = Line(map, Fields.Hp, star.Hp, errors);
            star.Attack = Line(map, Fields.Attack, star.Attack, errors);
            star.SwingSpeed = Line(map, Fields.SwingSpeed, star.SwingSpeed, errors);
            star.Speed = Line(map, Fields.Speed, star.Speed, errors);
            star.Drops = Line(map, Fields.Drops, star.Drops, errors);
        }

        private static float[] Line(YamlMappingNode map, string key, float[] current, List<string> errors)
        {
            float[]? values = ReadFloats(map, key, errors);
            return values ?? current;
        }

        private static void MutationChance(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            float[]? values = ReadFloats(block, ChanceKey, errors);
            if (values != null)
            {
                rules.MutationChance = values;
            }
        }

        private static void MutationChances(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            if (!(YamlRead.Child(block, ChancesKey) is YamlMappingNode map))
            {
                return;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                Mutation? mutation = NamedMutation(pair.Key, errors);
                float[]? values = YamlRead.Floats(pair.Value, errors, "a mutation chance");
                if (mutation != null && values != null)
                {
                    rules.MutationChances[mutation.Value] = values;
                }
            }
        }

        private static void MutationPower(BiomeRules rules, YamlMappingNode block, List<string> errors)
        {
            if (!(YamlRead.Child(block, PowerKey) is YamlMappingNode map))
            {
                return;
            }
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                Mutation? mutation = NamedMutation(pair.Key, errors);
                if (mutation != null && YamlRead.Map(pair.Value, errors, "a mutation's power") is YamlMappingNode fields)
                {
                    MergeFields(rules, mutation.Value, fields, errors);
                }
            }
        }

        private static void MergeFields(BiomeRules rules, Mutation mutation, YamlMappingNode fields, List<string> errors)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in fields.Children)
            {
                string? key = (pair.Key as YamlScalarNode)?.Value;
                if (key == null)
                {
                    YamlRead.AddError(errors, pair.Key, $"'{mutation}' has a power field with no name");
                }
                else if (Fields.IsPrefabField(key))
                {
                    MergeText(rules, mutation, key, pair.Value, errors);
                }
                else
                {
                    MergeNumber(rules, mutation, key, pair.Value, errors);
                }
            }
        }

        private static void MergeNumber(BiomeRules rules, Mutation m, string key, YamlNode node, List<string> errors)
        {
            if (!YamlRead.TryFloat(node, out float value))
            {
                YamlRead.AddError(errors, node, $"'{m}' field '{key}' is not a number");
                return;
            }
            if (!rules.MutationPower.TryGetValue(m, out Dictionary<string, float> into))
            {
                rules.MutationPower[m] = into = new Dictionary<string, float>();
            }
            into[key] = value;
        }

        private static void MergeText(BiomeRules rules, Mutation m, string key, YamlNode node, List<string> errors)
        {
            string? value = (node as YamlScalarNode)?.Value;
            if (string.IsNullOrEmpty(value))
            {
                YamlRead.AddError(errors, node, $"'{m}' field '{key}' should be a prefab name");
                return;
            }
            if (!rules.MutationText.TryGetValue(m, out Dictionary<string, string> into))
            {
                rules.MutationText[m] = into = new Dictionary<string, string>();
            }
            into[key] = value!;
        }

        private static Mutation? NamedMutation(YamlNode key, List<string> errors)
        {
            string name = (key as YamlScalarNode)?.Value ?? "";
            Mutation? mutation = MutationCatalog.FromName(name);
            if (mutation == null)
            {
                YamlRead.AddError(errors, key, $"'{name}' is not a mutation");
            }
            return mutation;
        }

        private static float[]? ReadFloats(YamlMappingNode block, string key, List<string> errors)
        {
            YamlNode? node = YamlRead.Child(block, key);
            return node == null ? null : YamlRead.Floats(node, errors, $"'{key}'");
        }
    }
}
