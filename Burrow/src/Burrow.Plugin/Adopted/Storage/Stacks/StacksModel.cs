using System.Collections.Generic;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Stacks
{
    /// <summary>
    /// The parsed Burrow.Stacks*.yml files: optional multipliers that replace the .cfg ones, the groups map and
    /// the items map as rules in file order.
    /// </summary>
    public sealed class StacksModel : YamlModel
    {
        private readonly ItemGroups groups = new ItemGroups();

        /// <summary>Replaces the .cfg Stack Multiplier when present.</summary>
        public float? StackMultiplier { get; private set; }

        /// <summary>Replaces the .cfg Weight Multiplier when present.</summary>
        public float? WeightMultiplier { get; private set; }

        public List<StackRule> Rules { get; } = new List<StackRule>();

        protected override void Read(YamlNode root)
        {
            if (root.Get("stack multiplier").TryFloat(out float stack))
                StackMultiplier = stack;
            if (root.Get("weight multiplier").TryFloat(out float weight))
                WeightMultiplier = weight;
            YamlNode groupsNode = root.Get("groups");
            if (groupsNode.Kind != YamlNodeKind.Null)
                groups.Read(groupsNode);
            YamlNode items = root.Get("items");
            if (items.Kind == YamlNodeKind.Null || items.Kind == YamlNodeKind.Missing)
                return;
            foreach (KeyValuePair<string, YamlNode> entry in items.Entries)
                ReadRule(entry.Key, entry.Value);
        }

        private void ReadRule(string key, YamlNode node)
        {
            ItemMatcher matcher = ItemMatcher.Parse(key, groups);
            if (matcher.Problem != null)
            {
                node.Warn("'" + key + "' is ignored: " + matcher.Problem);
                return;
            }
            int? stack = ReadStack(node.Get("stack"));
            float? weight = ReadWeight(node.Get("weight"));
            if (stack == null && weight == null)
                node.Warn("neither stack nor weight is set, the entry does nothing");
            else
                Rules.Add(new StackRule(matcher, stack, weight));
        }

        private static int? ReadStack(YamlNode node)
        {
            if (!node.TryInt(out int stack))
                return null;
            if (stack >= 1)
                return stack;
            node.Error("stack must be at least 1");
            return null;
        }

        private static float? ReadWeight(YamlNode node)
        {
            if (!node.TryFloat(out float weight))
                return null;
            if (weight >= 0f)
                return weight;
            node.Error("weight must not be negative");
            return null;
        }

        protected override void Verify()
        {
            if (StackMultiplier.HasValue && StackMultiplier.Value <= 0f)
                Errors.Add("stack multiplier must be greater than 0");
            if (WeightMultiplier.HasValue && WeightMultiplier.Value < 0f)
                Errors.Add("weight multiplier must not be negative");
        }
    }
}
