using System;
using System.Collections.Generic;
using Burrow.Core;
using Burrow.Shared;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// The containers an action may write to: the one the player has open and the ones within <c>Nearby Range</c>,
    /// nearest first, the open one first. A target is usable (the section 0 rules pass and the local client owns
    /// it or may claim it because nobody uses it) or shared (<c>Shared Chests</c> is <c>Full</c>, another player
    /// uses it and the rules pass; the writer sends requests to that player's client). A chest the player only
    /// views (<c>View</c> mode) is no target; the actions that need the open container say "Viewing only" for it.
    /// </summary>
    public static class StowTargets
    {
        /// <summary>The container open in the inventory panel, or null.</summary>
        public static Container Open => InventoryGui.instance != null ? InventoryGui.instance.m_currentContainer : null;

        /// <summary>The open container when it is a target, else null.</summary>
        public static Container OpenTarget
        {
            get
            {
                Container open = Open;
                return open != null && IsTarget(open) ? open : null;
            }
        }

        /// <summary>Usable now, or shared. Never throws.</summary>
        public static bool IsTarget(Container container)
        {
            try
            {
                return ContainerScan.IsUsable(container, ContainerUse.Stow) || ContainerScan.IsShared(container);
            }
            catch (Exception e)
            {
                Plugin.Log.LogDebug($"Burrow: container skipped, {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        /// <summary>Writes to this container go through requests to the client of the player using it.</summary>
        public static bool IsShared(Container container) => ChestWriter.IsShared(container);

        /// <summary>Targets within Nearby Range, nearest first; the open container leads when it is among them.</summary>
        public static List<Container> Nearby(Player player)
        {
            Vector3 origin = player.transform.position;
            float range = StowSettings.NearbyRange.Value;
            List<KeyValuePair<float, Container>> found = new List<KeyValuePair<float, Container>>();
            foreach (Container container in ContainerScan.All())
            {
                float distance = Distance(origin, container);
                if (distance <= range && IsTarget(container))
                    found.Add(new KeyValuePair<float, Container>(distance, container));
            }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            List<Container> list = found.ConvertAll(pair => pair.Value);
            Container open = Open;
            if (open != null && list.Remove(open))
                list.Insert(0, open);
            return list;
        }

        /// <summary>Only the open container, as a list, or an empty list.</summary>
        public static List<Container> OpenOnly()
        {
            List<Container> list = new List<Container>();
            Container open = OpenTarget;
            if (open != null)
                list.Add(open);
            return list;
        }

        /// <summary>
        /// True, and "Viewing only" is said, when the panel shows a chest the player views without owning it and
        /// <c>Shared Chests</c> does not allow requests (View mode).
        /// </summary>
        public static bool ViewingOnly()
        {
            Container open = Open;
            if (open == null || !SharedState.IsViewing(open) || SharedState.FullMode)
                return false;
            Messages.Center(SharedWords.ReadOnly);
            return true;
        }

        /// <summary>Says why the open container is no target: "Viewing only", else "No container is open".</summary>
        public static void SayNoOpen()
        {
            if (!ViewingOnly())
                Messages.Center(StowWords.NoContainer);
        }

        /// <summary>Distance from the origin, or infinity for a container that is gone.</summary>
        public static float Distance(Vector3 origin, Container container)
        {
            try
            {
                return Vector3.Distance(origin, container.transform.position);
            }
            catch (Exception)
            {
                return float.PositiveInfinity;
            }
        }

        public static string Name(Container container)
        {
            Inventory inventory = container != null ? container.GetInventory() : null;
            return inventory != null ? Language.Localize(inventory.GetName()) : "";
        }
    }
}
