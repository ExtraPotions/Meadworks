using System;
using System.Collections.Generic;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Core
{
    /// <summary>
    /// The <c>groups:</c> map of one YAML file: a name to a list of vocabulary entries, which may name other groups.
    /// Matching recurses through group members with a visited set, so cycles are tolerated. <see cref="Empty"/> is
    /// the shared instance for matchers parsed without a file; never call <see cref="Read"/> on it.
    /// </summary>
    public sealed class ItemGroups
    {
        private readonly Dictionary<string, List<ItemMatcher>> groups = new Dictionary<string, List<ItemMatcher>>(StringComparer.OrdinalIgnoreCase);

        public static ItemGroups Empty { get; } = new ItemGroups();

        public IEnumerable<string> Names => groups.Keys;

        /// <summary>
        /// Reads a <c>groups:</c> node: name to list of matcher strings. A node that is not a map, a member list that
        /// is not a list of scalars and a name without a value are errors on the model (the file is rejected);
        /// an entry the vocabulary does not understand or a member naming an unknown group is a warning.
        /// </summary>
        public void Read(YamlNode groupsNode)
        {
            groups.Clear();
            if (groupsNode == null || groupsNode.Kind == YamlNodeKind.Missing)
                return;
            foreach (KeyValuePair<string, YamlNode> entry in groupsNode.Entries)
            {
                if (!entry.Value.TryStringList(out List<string> members))
                    continue;
                groups[entry.Key] = ParseMembers(entry.Value, members);
            }
            WarnUnknownGroups(groupsNode);
        }

        private List<ItemMatcher> ParseMembers(YamlNode node, List<string> members)
        {
            List<ItemMatcher> matchers = new List<ItemMatcher>();
            foreach (string member in members)
            {
                ItemMatcher matcher = ItemMatcher.Parse(member, this);
                if (matcher.Problem != null)
                    node.Warn("'" + member + "' is ignored: " + matcher.Problem);
                else
                    matchers.Add(matcher);
            }
            return matchers;
        }

        private void WarnUnknownGroups(YamlNode groupsNode)
        {
            foreach (KeyValuePair<string, List<ItemMatcher>> group in groups)
            {
                foreach (ItemMatcher member in group.Value)
                {
                    if (member.GroupName != null && !groups.ContainsKey(member.GroupName))
                        groupsNode.Get(group.Key).Warn("'" + member + "' names a group that does not exist");
                }
            }
        }

        public bool Matches(string group, ItemDrop.ItemData item)
        {
            return item != null && item.m_shared != null && Matches(group, ItemNames.PrefabName(item), item.m_shared);
        }

        public bool Matches(string group, string prefabName, ItemDrop.ItemData.SharedData shared)
        {
            return Matches(group, prefabName, shared, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private bool Matches(string group, string prefabName, ItemDrop.ItemData.SharedData shared, HashSet<string> visited)
        {
            if (group == null || !groups.TryGetValue(group, out List<ItemMatcher> members) || !visited.Add(group))
                return false;
            foreach (ItemMatcher member in members)
            {
                bool hit = member.GroupName != null
                    ? Matches(member.GroupName, prefabName, shared, visited)
                    : member.Matches(prefabName, shared);
                if (hit)
                    return true;
            }
            return false;
        }
    }
}
