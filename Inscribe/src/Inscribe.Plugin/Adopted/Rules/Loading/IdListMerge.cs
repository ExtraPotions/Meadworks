// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.10.0 at d3b57606cb24775d61b7bda244b22eb89e7e7717.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Collections.Generic;
using YamlDotNet.RepresentationModel;

namespace ExtraPotions.Inscribe.Adopted.Rules
{
    /// <summary>
    /// Merges a list of entries with an <c>id</c> (affixes, rarities, stones, item classes): a new id is appended, an existing id
    /// changes only the fields the later entry names (logged as a note, so an owner can see what their files did), and
    /// the same id twice in one file is an error. Entries without an id pass through for the parser to report.
    /// </summary>
    internal static class IdListMerge
    {
        public static YamlSequenceNode Merge(YamlSequenceNode? earlier, YamlSequenceNode later, string listKey,
            SourceLayer layer, RuleIssues issues)
        {
            List<YamlNode> result = earlier != null ? new List<YamlNode>(earlier.Children) : new List<YamlNode>();
            HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (YamlNode entry in later.Children)
            {
                string? id = EntryId(entry);
                if (id == null)
                {
                    result.Add(entry);
                    continue;
                }
                if (!seen.Add(id))
                {
                    issues.Error($"{listKey}[{id}]", entry, $"the id '{id}' appears twice in {layer.Origin}");
                    continue;
                }
                MergeEntry(result, (YamlMappingNode)entry, id, listKey, layer, issues);
            }
            return new YamlSequenceNode(result);
        }

        private static void MergeEntry(List<YamlNode> result, YamlMappingNode entry, string id, string listKey,
            SourceLayer layer, RuleIssues issues)
        {
            int index = result.FindIndex(n => EntryId(n) == id);
            if (index < 0)
            {
                result.Add(entry);
                return;
            }
            YamlMappingNode earlier = (YamlMappingNode)result[index];
            result[index] = YamlMerge.MergeMaps(earlier, entry);
            List<string> changed = ChangedFields(earlier, entry);
            if (!layer.BuiltIn && changed.Count > 0)
            {
                issues.Note($"{listKey} '{id}' overridden by {layer.Origin}: {string.Join(", ", changed)}");
            }
        }

        public static string? EntryId(YamlNode node) =>
            node is YamlMappingNode map ? YamlNodes.Text(YamlNodes.Child(map, "id")) : null;

        /// <summary>The fields the later entry really changes: a main file holding the defaults word for word is no override.</summary>
        private static List<string> ChangedFields(YamlMappingNode earlier, YamlMappingNode entry)
        {
            List<string> keys = new List<string>();
            foreach (KeyValuePair<string, YamlNode> pair in YamlLists.Pairs(entry))
            {
                if (pair.Key != "id" && !YamlNodes.Same(YamlNodes.Child(earlier, pair.Key), pair.Value))
                {
                    keys.Add(pair.Key);
                }
            }
            return keys;
        }
    }
}
