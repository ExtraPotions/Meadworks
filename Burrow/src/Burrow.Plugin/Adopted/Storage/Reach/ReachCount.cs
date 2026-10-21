using System;
using System.Collections.Generic;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// The reachable containers of the local player and what they hold. The container list is found once per
    /// frame (the panels ask for every row every frame); contents are always read live. Every count honours the
    /// prefab's allow / deny lists per stack and the game's quality and world level filters.
    /// </summary>
    public static class ReachCount
    {
        private static int cachedFrame = -1;
        private static List<Container> cached = new List<Container>();

        /// <summary>Every reachable container, nearest first: section 0 rules, the enabled table and the per prefab range.</summary>
        public static List<Container> Containers()
        {
            if (Time.frameCount == cachedFrame)
                return cached;
            cachedFrame = Time.frameCount;
            cached = Find();
            return cached;
        }

        private static List<Container> Find()
        {
            List<Container> result = new List<Container>();
            Player player = Player.m_localPlayer;
            if (player == null)
                return result;
            Vector3 position = player.transform.position;
            foreach (Container container in ContainerScan.Nearby(position, ReachRules.MaxRange(), ContainerUse.Reach))
            {
                if (Vector3.Distance(position, container.transform.position) <= ReachRules.RangeFor(container))
                    result.Add(container);
            }
            return result;
        }

        /// <summary>The game's requirement filter: shared name, quality (-1 for any) and the world level rule.</summary>
        public static bool Matches(ItemDrop.ItemData item, string name, int quality, bool worldLevel)
        {
            if (item == null || item.m_shared == null || item.m_shared.m_name != name)
                return false;
            if (quality >= 0 && item.m_quality != quality)
                return false;
            return !worldLevel || item.m_worldLevel >= Game.m_worldLevel;
        }

        /// <summary>Items of a shared name in every reachable container.</summary>
        public static int InContainers(string name, int quality, bool worldLevel)
        {
            int sum = 0;
            foreach (Container container in Containers())
                sum += CountIn(container, name, quality, worldLevel);
            return sum;
        }

        public static int CountIn(Container container, string name, int quality, bool worldLevel)
        {
            if (!Ready(container))
                return 0;
            // The same count as CountIn(container, item => Matches(...)) without a closure per call (every row, every frame).
            ContainerRule rule = ReachRules.RuleFor(container);
            int sum = 0;
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (Matches(item, name, quality, worldLevel) && rule.Accepts(item))
                    sum += item.m_stack;
            }
            return sum;
        }

        /// <summary>Items in every reachable container that a station would accept (the game's world level rule applies).</summary>
        public static int CountMatching(Func<ItemDrop.ItemData, bool> accepts)
        {
            int sum = 0;
            foreach (Container container in Containers())
                sum += CountIn(container, item => item.m_worldLevel >= Game.m_worldLevel && accepts(item));
            return sum;
        }

        public static int CountIn(Container container, Func<ItemDrop.ItemData, bool> wanted)
        {
            if (!Ready(container))
                return 0;
            ContainerRule rule = ReachRules.RuleFor(container);
            int sum = 0;
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (wanted(item) && rule.Accepts(item))
                    sum += item.m_stack;
            }
            return sum;
        }

        /// <summary>The first stack of a shared name in the reachable containers, nearest container first.</summary>
        public static ItemDrop.ItemData FirstInContainers(string name, int quality, bool worldLevel)
        {
            foreach (Container container in Containers())
            {
                ItemDrop.ItemData found = FirstIn(container, item => Matches(item, name, quality, worldLevel));
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>The first stack the container holds that the caller wants and the prefab's lists allow.</summary>
        public static ItemDrop.ItemData FirstIn(Container container, Func<ItemDrop.ItemData, bool> wanted)
        {
            if (!Ready(container))
                return null;
            ContainerRule rule = ReachRules.RuleFor(container);
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (wanted(item) && rule.Accepts(item))
                    return item;
            }
            return null;
        }

        private static bool Ready(Container container)
        {
            if (ContainerScan.CanClaimNow(container))
                return true;
            // Remote stock stays unavailable until the owner hands over its current inventory.
            ContainerScan.Claim(container);
            return false;
        }
    }
}
