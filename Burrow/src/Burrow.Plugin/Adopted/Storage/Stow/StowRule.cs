using System.Collections.Generic;
using Burrow.Core;
using ExtraPotions.Burrow.Embedded.YamlConfig;

namespace Burrow.Stow
{
    /// <summary>One <c>containers:</c> entry of Burrow.Stow.yml: whether the prefab picks items up from the ground,
    /// what it accepts even when it does not hold it yet, and what it refuses (never taken, never routed here).</summary>
    public sealed class StowRule
    {
        public static StowRule Default { get; } = new StowRule(false, ItemMatchSet.Empty, ItemMatchSet.Empty);

        private StowRule(bool pickup, ItemMatchSet accept, ItemMatchSet refuse)
        {
            Pickup = pickup;
            Accept = accept;
            Refuse = refuse;
        }

        public bool Pickup { get; }
        public ItemMatchSet Accept { get; }
        public ItemMatchSet Refuse { get; }

        public static StowRule Read(YamlNode node, ItemGroups groups)
        {
            if (node.Kind != YamlNodeKind.Map)
            {
                node.Error("a container entry needs a map with pickup, accept or refuse");
                return Default;
            }
            bool pickup = false;
            node.Get("pickup").TryBool(out pickup);
            return new StowRule(pickup, ReadList(node.Get("accept"), groups), ReadList(node.Get("refuse"), groups));
        }

        private static ItemMatchSet ReadList(YamlNode node, ItemGroups groups)
        {
            if (node.Kind == YamlNodeKind.Missing || !node.TryStringList(out List<string> entries))
                return ItemMatchSet.Empty;
            ItemMatchSet set = ItemMatchSet.Parse(entries, groups);
            foreach (ItemMatcher matcher in set.Matchers)
            {
                if (matcher.Problem != null)
                    node.Warn("'" + matcher + "' is ignored: " + matcher.Problem);
            }
            return set;
        }
    }
}
