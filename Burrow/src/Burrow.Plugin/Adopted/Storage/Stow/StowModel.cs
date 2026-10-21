using System;
using System.Collections.Generic;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Stow
{
    /// <summary>The model of Burrow.Stow*.yml: the <c>groups:</c> map and the <c>containers:</c> rules by prefab.</summary>
    public sealed class StowModel : YamlModel
    {
        public ItemGroups ItemGroups { get; } = new ItemGroups();

        public Dictionary<string, StowRule> Containers { get; } = new Dictionary<string, StowRule>(StringComparer.OrdinalIgnoreCase);

        protected override void Read(YamlNode root)
        {
            ItemGroups.Read(root.Get("groups"));
            YamlNode containers = root.Get("containers");
            if (containers.Kind == YamlNodeKind.Missing)
                return;
            foreach (KeyValuePair<string, YamlNode> entry in containers.Entries)
                Containers[entry.Key.Trim()] = StowRule.Read(entry.Value, ItemGroups);
        }
    }
}
