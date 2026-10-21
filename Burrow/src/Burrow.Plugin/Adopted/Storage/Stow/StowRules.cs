using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>The applied Burrow.Stow.yml: rule lookup by container, the refuse and accept checks and the groups
    /// used for "same group" routing. Before a file applies every container has the default rule and no groups.</summary>
    public static class StowRules
    {
        private static StowModel current = new StowModel();

        public static ItemGroups Groups => current.ItemGroups;

        public static void Apply(StowModel model)
        {
            current = model ?? new StowModel();
        }

        public static StowRule For(Container container)
        {
            string prefab = ContainerScan.PrefabName(container);
            return current.Containers.TryGetValue(prefab, out StowRule rule) ? rule : StowRule.Default;
        }

        /// <summary>The container's refuse list names the item: never taken, never routed here.</summary>
        public static bool Refuses(Container container, ItemDrop.ItemData item)
        {
            StowRule rule = For(container);
            return !rule.Refuse.IsEmpty && rule.Refuse.Matches(item);
        }

        /// <summary>The container's accept list names the item and its refuse list does not.</summary>
        public static bool Accepts(Container container, ItemDrop.ItemData item)
        {
            StowRule rule = For(container);
            return !rule.Accept.IsEmpty && rule.Accept.Matches(item) && !Refuses(container, item);
        }

        public static bool PicksUp(Container container) => For(container).Pickup;

        /// <summary>The group names the item belongs to, in file order.</summary>
        public static List<string> GroupsOf(ItemDrop.ItemData item)
        {
            List<string> names = new List<string>();
            foreach (string group in current.ItemGroups.Names)
            {
                if (current.ItemGroups.Matches(group, item))
                    names.Add(group);
            }
            return names;
        }
    }
}
