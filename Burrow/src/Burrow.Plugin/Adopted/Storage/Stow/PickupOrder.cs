using Burrow.Core;
using UnityEngine;

namespace Burrow.Stow
{
    /// <summary>
    /// Which pickup chest takes a drop (the user's rule for every automatic store, 2026-09-29: "it should try to store
    /// in the chest closest to it. if that chest is full then the next closest"). Among the pickup chests whose range
    /// reaches the drop and that would take it now (a pickup chest, not in use, wanting the item, room for one more), a
    /// chest that already holds the item comes before one that only accepts it, and a nearer one before a farther one,
    /// measured from the drop. A chest sweeps a drop only when none ranks before it; a full chest has no room, so the
    /// next one takes the drop on its own sweep. Every chest sweeps on its own owner's client, so each owner decides from
    /// the same replicated contents; a drop a nearer chest will take simply waits for that chest's next sweep. So a
    /// kiln's coal dropping at its output goes into the nearest pickup chest holding coal, and on when that one is full.
    /// </summary>
    internal static class PickupOrder
    {
        /// <summary>A chest that holds the item ranks this far ahead of one that only accepts it, whatever the distances.</summary>
        private const float HolderLead = 100000f;

        /// <summary>True when no other pickup chest in reach of the drop ranks before <paramref name="container"/> and can take it now.</summary>
        public static bool IsFirst(Container container, ItemDrop drop)
        {
            ItemDrop.ItemData item = drop.m_itemData;
            Vector3 at = drop.transform.position;
            float rank = Rank(container, item, at);
            foreach (Container rival in ContainerScan.Nearby(at, StowSettings.PickupRange.Value, ContainerUse.Stow))
            {
                if (rival == container)
                    continue;
                float other = Rank(rival, item, at);
                if ((other < rank || other == rank && Before(rival, container)) && TakesNow(rival, item))
                    return false;
            }
            return true;
        }

        /// <summary>Breaks an equal rank by the chests' ids, so every owner orders the same two chests the same way.</summary>
        private static bool Before(Container a, Container b)
        {
            ZDOID x = a.m_nview != null && a.m_nview.GetZDO() != null ? a.m_nview.GetZDO().m_uid : ZDOID.None;
            ZDOID y = b.m_nview != null && b.m_nview.GetZDO() != null ? b.m_nview.GetZDO().m_uid : ZDOID.None;
            return x.UserID != y.UserID ? x.UserID < y.UserID : x.ID < y.ID;
        }

        /// <summary>Holders first, then nearer first: the lower, the sooner.</summary>
        private static float Rank(Container container, ItemDrop.ItemData item, Vector3 at)
        {
            bool holds = container.GetInventory().ContainsItemByName(item.m_shared.m_name);
            return (holds ? 0f : HolderLead) + Vector3.Distance(container.transform.position, at);
        }

        /// <summary>The rival would sweep this item up on its own turn: a pickup chest, free, wanting it, with room for one.</summary>
        private static bool TakesNow(Container rival, ItemDrop.ItemData item) =>
            StowRules.PicksUp(rival) && !rival.IsInUse() && GroundPickup.Wanted(rival, item) && rival.GetInventory().CanAddItem(item, 1);
    }
}
