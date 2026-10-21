// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Merges the layers of a family into one document (configuration.md section 3): maps merge by key, recursively;
    /// the id lists (<c>inscriptions</c>, <c>rarities</c>, <c>runes</c>, <c>classes</c>) merge by <c>id</c>, field by field; every other list and every scalar is replaced whole by the
    /// later layer; a key set to null in a later layer is removed. Inputs are never modified: merged maps are new
    /// nodes, adopted values keep their original nodes (and so their file and line for error messages).
    /// </summary>
    internal static class YamlMerge
    {
        public static YamlMappingNode Merge(IReadOnlyList<SourceLayer> layers, string[] idLists, RuleIssues issues)
        {
            YamlMappingNode merged = new YamlMappingNode();
            foreach (SourceLayer layer in layers)
            {
                merged = MergeRoot(merged, layer, idLists, issues);
            }
            return merged;
        }

        private static YamlMappingNode MergeRoot(YamlMappingNode a, SourceLayer layer, string[] idLists, RuleIssues issues) =>
            MergeAt(a, layer.Root, "", new MergeScope(layer, idLists, issues));

        // Maps at `path` merge key by key; an id list named by its dotted path (`runes`, `rarities`) merges by
        // id; a map holding such a list further down keeps descending; everything else is MergeValue.
        private static YamlMappingNode MergeAt(YamlMappingNode a, YamlMappingNode b, string path, MergeScope scope)
        {
            List<KeyValuePair<string, YamlNode>> result = Entries(a);
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(b))
            {
                string at = path.Length == 0 ? pair.Key : path + "." + pair.Key;
                YamlNode? previous = Find(result, pair.Key);
                YamlNode? value;
                if (System.Array.IndexOf(scope.IdLists, at) >= 0 && pair.Value is YamlSequenceNode seq)
                {
                    value = IdListMerge.Merge(previous as YamlSequenceNode, seq, at, scope.Layer, scope.Issues);
                }
                else if (pair.Value is YamlMappingNode map && scope.HoldsIdList(at))
                {
                    value = MergeAt(previous as YamlMappingNode ?? new YamlMappingNode(), map, at, scope);
                }
                else
                {
                    value = MergeValue(previous, pair.Value);
                }
                Set(result, pair.Key, value);
            }
            return Build(result);
        }

        private sealed class MergeScope
        {
            public MergeScope(SourceLayer layer, string[] idLists, RuleIssues issues)
            {
                Layer = layer;
                IdLists = idLists;
                Issues = issues;
            }

            public SourceLayer Layer { get; }
            public string[] IdLists { get; }
            public RuleIssues Issues { get; }

            /// <summary>Whether an id list lives somewhere below this dotted path.</summary>
            public bool HoldsIdList(string path)
            {
                foreach (string list in IdLists)
                {
                    if (list.StartsWith(path + ".", System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Merges a later value over an earlier one: maps by key, anything else replaced; null removes.</summary>
        public static YamlNode? MergeValue(YamlNode? earlier, YamlNode later)
        {
            if (YamlNodes.IsNull(later))
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
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(b))
            {
                Set(result, pair.Key, MergeValue(Find(result, pair.Key), pair.Value));
            }
            return Build(result);
        }

        private static List<KeyValuePair<string, YamlNode>> Entries(YamlMappingNode map) =>
            new List<KeyValuePair<string, YamlNode>>(YamlLists.Pairs(map));

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
    }

    /// <summary>One parsed layer: its root mapping and the name it is reported under.</summary>
    internal sealed class SourceLayer
    {
        public SourceLayer(string origin, YamlMappingNode root, bool builtIn)
        {
            Origin = origin;
            Root = root;
            BuiltIn = builtIn;
        }

        public string Origin { get; }
        public YamlMappingNode Root { get; }
        public bool BuiltIn { get; }
    }
}
