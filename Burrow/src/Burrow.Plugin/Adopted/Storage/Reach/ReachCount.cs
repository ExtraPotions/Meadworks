using System;
using System.Collections.Generic;
using Burrow.Core;
using HarmonyLib;
using UnityEngine;

namespace Burrow.Reach
{
    /// <summary>
    /// The reachable containers of the local player and what they hold. The container list is found once per
    /// frame (the panels ask for every row every frame). Whether a container may be counted (ownership, holds, use)
    /// is checked live on every ask. What a ready container holds of a name is kept for the rest of the frame and
    /// dropped at once when any inventory changes, so a pull or payment always counts again. Every count honours
    /// the prefab's allow / deny lists per stack and the game's quality and world level filters.
    /// </summary>
    public static class ReachCount
    {
        private static int cachedFrame = -1;
        private static List<Container> cached = new List<Container>();

        // The crafting panel asks for the same names from every requirement row, from HaveRequirementItems and, when
        // the recipe list is rebuilt, once per known recipe; each ask used to read every stack of every chest again.
        private static int storedFrame = -1;
        private static int storedVersion = -1;
        private static int version;
        private static readonly Dictionary<(Container, string, int, bool), int> stored = new Dictionary<(Container, string, int, bool), int>();

        /// <summary>An inventory changed somewhere: stored counts are read again.</summary>
        internal static void Invalidate()
        {
            unchecked { version++; }
        }

        private static int Stored(Container container, string name, int quality, bool worldLevel)
        {
            if (Time.frameCount != storedFrame || version != storedVersion)
            {
                storedFrame = Time.frameCount;
                storedVersion = version;
                stored.Clear();
            }
            var key = (container, name, quality, worldLevel);
            if (!stored.TryGetValue(key, out int sum))
                stored[key] = sum = Holds(container, name, quality, worldLevel);
            return sum;
        }

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
            {
                if (Ready(container))
                    sum += Stored(container, name, quality, worldLevel);
            }
            return sum;
        }

        /// <summary>Read live: payments and pulls use this between their own changes.</summary>
        public static int CountIn(Container container, string name, int quality, bool worldLevel)
        {
            return Ready(container) ? Holds(container, name, quality, worldLevel) : 0;
        }

        private static int Holds(Container container, string name, int quality, bool worldLevel)
        {
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

    /// <summary>Any inventory change (a pull, a payment, a handover load) drops this frame's stored counts.</summary>
    [HarmonyPatch(typeof(Inventory), "Changed")]
    public static class ReachCountInvalidate
    {
        [HarmonyPostfix]
        public static void Postfix() => ReachCount.Invalidate();
    }
}
