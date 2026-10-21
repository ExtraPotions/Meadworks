using System;
using System.Collections.Generic;
using HarmonyLib;
using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Ground pickup (SPEC 2.2), hooked on the container's own once-a-second <c>CheckForChanges</c>: on the client
    /// that owns the container's ZDO, once per <c>Pickup Interval</c> (staggered by instance id), never while the
    /// container is in use, for prefabs with <c>pickup: true</c>. Dropped items within <c>Pickup Range</c> that have
    /// lain longer than <c>Pickup Delay</c> (the ZDO's spawn time), are wanted by the rule and are not inside a ward
    /// the local player may not use are claimed, added with the game's add method and destroyed through the scene,
    /// or reduced by what fitted. Items that were placed as pieces are left alone. When several pickup chests reach a
    /// drop, it is left to the one that ranks first (<see cref="PickupOrder"/>: a chest holding the item before one
    /// that only accepts it, then the nearest to the drop), and to the next when that one is full.
    /// </summary>
    [HarmonyPatch(typeof(Container), nameof(Container.CheckForChanges))]
    public static class GroundPickup
    {
        private static readonly Dictionary<Container, float> nextSweep = new Dictionary<Container, float>();

        [HarmonyPostfix]
        public static void Postfix(Container __instance)
        {
            Tick(__instance);
        }

        public static void Tick(Container container)
        {
            if (!StowSettings.Enabled.Value || !StowSettings.GroundPickup.Value || container == null)
                return;
            ZNetView view = container.m_nview;
            if (view == null || !view.IsValid() || !view.IsOwner() || container.IsInUse() || !StowRules.PicksUp(container))
                return;
            if (!Due(container) || Player.m_localPlayer == null || ZNetScene.instance == null)
                return;
            if (!ContainerScan.IsUsable(container, ContainerUse.Stow))
                return;
            if (Sweep(container) > 0)
                ContainerScan.Save(container);
        }

        private static bool Due(Container container)
        {
            float now = Time.time;
            float interval = Mathf.Max(1f, StowSettings.PickupInterval.Value);
            if (!nextSweep.TryGetValue(container, out float due))
            {
                nextSweep[container] = now + interval * ((container.GetInstanceID() & 0xFF) / 256f);
                if (nextSweep.Count > 64)
                    Prune();
                return false;
            }
            if (now < due)
                return false;
            nextSweep[container] = now + interval;
            return true;
        }

        private static void Prune()
        {
            List<Container> gone = new List<Container>();
            foreach (Container key in nextSweep.Keys)
            {
                if (key == null)
                    gone.Add(key);
            }
            foreach (Container key in gone)
                nextSweep.Remove(key);
        }

        private static int Sweep(Container container)
        {
            Vector3 origin = container.transform.position;
            float range = StowSettings.PickupRange.Value;
            int taken = 0;
            foreach (ItemDrop drop in new List<ItemDrop>(ItemDrop.s_instances))
            {
                if (drop != null && Eligible(container, drop, origin, range))
                    taken += Take(container, drop);
            }
            return taken;
        }

        private static bool Eligible(Container container, ItemDrop drop, Vector3 origin, float range)
        {
            ZNetView view = drop.m_nview;
            if (view == null || !view.IsValid() || drop.m_itemData == null || drop.m_itemData.m_shared == null || drop.IsPiece())
                return false;
            if (Vector3.Distance(origin, drop.transform.position) > range)
                return false;
            if (Age(view) < StowSettings.PickupDelay.Value || !Wanted(container, drop.m_itemData))
                return false;
            if (CoreSettings.HonourWards.Value && !PrivateArea.CheckAccess(drop.transform.position, 0f, false))
                return false;
            return PickupOrder.IsFirst(container, drop);
        }

        /// <summary>Seconds since the drop's ZDO spawn time; negative when the time is unknown.</summary>
        private static double Age(ZNetView view)
        {
            long ticks = view.GetZDO().GetLong(ZDOVars.s_spawnTime, 0L);
            if (ticks == 0L || ZNet.instance == null)
                return -1.0;
            return (ZNet.instance.GetTime() - new DateTime(ticks)).TotalSeconds;
        }

        /// <summary>The container's rules want the item from the ground: not refused, and accepted or (with Pickup Only Held Items) already held.</summary>
        internal static bool Wanted(Container container, ItemDrop.ItemData item)
        {
            if (StowRules.Refuses(container, item))
                return false;
            if (StowRules.Accepts(container, item))
                return true;
            return !StowSettings.PickupOnlyHeldItems.Value || container.GetInventory().ContainsItemByName(item.m_shared.m_name);
        }

        private static int Take(Container container, ItemDrop drop)
        {
            ZNetView view = drop.m_nview;
            if (!view.IsOwner())
                view.ClaimOwnership();
            if (!view.IsOwner())
                return 0;
            drop.Load();
            ItemDrop.ItemData data = drop.m_itemData.Clone();
            int before = data.m_stack;
            if (before <= 0)
                return 0;
            if (container.GetInventory().AddItem(data))
            {
                ZNetScene.instance.Destroy(drop.gameObject);
                return before;
            }
            int taken = before - data.m_stack;
            if (taken > 0)
            {
                drop.m_itemData.m_stack = data.m_stack;
                drop.Save();
            }
            return taken;
        }
    }
}
