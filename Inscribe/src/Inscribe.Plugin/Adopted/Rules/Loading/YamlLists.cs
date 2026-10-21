// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>List and map reads that are not tied to one mapping key: number rows, id → number maps.</summary>
    internal static class YamlLists
    {
        /// <summary>A list of numbers, each at least 0 when <paramref name="nonNegative"/>; length checked when above 0.</summary>
        public static float[]? Floats(YamlNode node, string path, RuleIssues issues, int length = 0, bool nonNegative = true)
        {
            if (!(node is YamlSequenceNode seq))
            {
                issues.Error(path, node, "should be a list of numbers like [1, 2, 3]");
                return null;
            }
            List<float> values = new List<float>();
            foreach (YamlNode item in seq.Children)
            {
                if (!YamlNodes.TryFloat(item, out float v) || (nonNegative && v < 0f))
                {
                    issues.Error(path, item, nonNegative ? "should be a number of at least 0" : "should be a number");
                    return null;
                }
                values.Add(v);
            }
            if (length > 0 && values.Count != length)
            {
                issues.Error(path, node, $"should have exactly {length} entries (tier 1 first), has {values.Count}");
                return null;
            }
            return values.ToArray();
        }

        /// <summary>
        /// A list indexed by tier: 8 numbers, tier 1 first (classes-and-tiers.md section 2). A list of 7 (format 1, before
        /// Deep North) still loads with its last value repeated for tier 8.
        /// </summary>
        public static float[]? TierFloats(YamlNode node, string path, RuleIssues issues, int tiers)
        {
            float[]? values = Floats(node, path, issues);
            if (values == null || values.Length == tiers)
            {
                return values;
            }
            if (values.Length == tiers - 1 && values.Length > 0)
            {
                float[] padded = new float[tiers];
                System.Array.Copy(values, padded, values.Length);
                padded[tiers - 1] = values[values.Length - 1];
                return padded;
            }
            issues.Error(path, node, $"should have {tiers} entries, tier 1 first (a list of {tiers - 1} still loads), has {values.Length}");
            return null;
        }

        /// <summary>Every (key, value node) of a mapping, keys as text.</summary>
        public static IEnumerable<KeyValuePair<string, YamlNode>> Pairs(YamlMappingNode map)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                yield return new KeyValuePair<string, YamlNode>(YamlNodes.KeyText(pair.Key), pair.Value);
            }
        }

        /// <summary>A map of name → whole number in [min, max]; bad entries are errors and skipped.</summary>
        public static Dictionary<string, int> IntMap(MapReader parent, string key, int min, int max)
        {
            Dictionary<string, int> map = new Dictionary<string, int>(System.StringComparer.Ordinal);
            MapReader? sub = parent.Sub(key);
            if (sub == null)
            {
                return map;
            }
            foreach (KeyValuePair<string, YamlNode> pair in Pairs(sub.Value.Map))
            {
                if (YamlNodes.TryInt(pair.Value, out int v) && v >= min && v <= max)
                {
                    map[pair.Key] = v;
                }
                else
                {
                    parent.Issues.Error(sub.Value.At(pair.Key), pair.Value, $"should be a whole number from {min} to {max}");
                }
            }
            return map;
        }

        /// <summary>A map of name → number of at least 0; bad entries are errors and skipped.</summary>
        public static Dictionary<string, float> FloatMap(MapReader parent, string key)
        {
            Dictionary<string, float> map = new Dictionary<string, float>(System.StringComparer.Ordinal);
            MapReader? sub = parent.Sub(key);
            if (sub == null)
            {
                return map;
            }
            foreach (KeyValuePair<string, YamlNode> pair in Pairs(sub.Value.Map))
            {
                if (YamlNodes.TryFloat(pair.Value, out float v) && v >= 0f)
                {
                    map[pair.Key] = v;
                }
                else
                {
                    parent.Issues.Error(sub.Value.At(pair.Key), pair.Value, "should be a number of at least 0");
                }
            }
            return map;
        }
    }
}
