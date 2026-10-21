// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Edge namespace, map/scalar subset from the pinned EliteCrafting YAML merge, via the Inscribe adaptation.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;
namespace ExtraPotions.Edge.Secondaries.Adopted;
internal static class RuleMapMerge
{
        public static YamlNode? MergeValue(YamlNode? earlier, YamlNode later)
        {
            if (IsNull(later))
            {
                return null;
            }
            if (earlier is YamlMappingNode a && later is YamlMappingNode b)
            {
                return MergeMaps(a, b);
            }
            return later is YamlMappingNode map ? MergeMaps(new YamlMappingNode(), map) : later;
        }

        public static YamlMappingNode MergeMaps(YamlMappingNode a, YamlMappingNode b)
        {
            List<KeyValuePair<string, YamlNode>> result = Entries(a);
            foreach (KeyValuePair<string, YamlNode> pair in Pairs(b))
            {
                Set(result, pair.Key, MergeValue(Find(result, pair.Key), pair.Value));
            }
            return Build(result);
        }

        private static List<KeyValuePair<string, YamlNode>> Entries(YamlMappingNode map) =>
            new List<KeyValuePair<string, YamlNode>>(Pairs(map));

        private static YamlNode? Find(List<KeyValuePair<string, YamlNode>> entries, string key)
        {
            foreach (KeyValuePair<string, YamlNode> entry in entries)
            {
                if (entry.Key == key)
                {
                    return entry.Value;
                }
            }
            return null;
        }

        private static void Set(List<KeyValuePair<string, YamlNode>> entries, string key, YamlNode? value)
        {
            int index = entries.FindIndex(e => e.Key == key);
            if (value == null)
            {
                if (index >= 0)
                {
                    entries.RemoveAt(index);
                }
                return;
            }
            KeyValuePair<string, YamlNode> entry = new KeyValuePair<string, YamlNode>(key, value);
            if (index >= 0)
            {
                entries[index] = entry;
            }
            else
            {
                entries.Add(entry);
            }
        }

        private static YamlMappingNode Build(List<KeyValuePair<string, YamlNode>> entries)
        {
            YamlMappingNode map = new YamlMappingNode();
            foreach (KeyValuePair<string, YamlNode> entry in entries)
            {
                map.Add(new YamlScalarNode(entry.Key), entry.Value);
            }
            return map;
        }
        public static bool IsNull(YamlNode? node)
        {
            if (!(node is YamlScalarNode scalar) || scalar.Style == YamlDotNet.Core.ScalarStyle.SingleQuoted
                || scalar.Style == YamlDotNet.Core.ScalarStyle.DoubleQuoted)
            {
                return false;
            }
            string? v = scalar.Value;
            return string.IsNullOrEmpty(v) || v == "~" || v == "null" || v == "Null" || v == "NULL";
        }

        public static IEnumerable<KeyValuePair<string, YamlNode>> Pairs(YamlMappingNode map)
        {
            foreach (KeyValuePair<YamlNode, YamlNode> pair in map.Children)
            {
                yield return new KeyValuePair<string, YamlNode>((pair.Key as YamlScalarNode)?.Value ?? "", pair.Value);
            }
        }

}
