using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Stow
{
    /// <summary>
    /// Route (modifier + click): the stack goes to the nearest nearby container that holds the exact item and has room;
    /// when none does, to the container the player has open; then one holding an item of the same group, then one whose
    /// <c>accept</c> list matches. Full containers are passed over. The open container leads the candidates, so the
    /// game's own modifier click target wins a tie; what the holders cannot take ends in the open container. Store one (its key):
    /// one item goes to the open container, else to the nearest container holding it. When the chosen container
    /// fills up, the rest goes on to the next nearby container holding the item, nearest first (<see cref="Overflow"/>).
    /// Both go through the writer: a shared target answers later, and the message waits for the last answer.
    /// </summary>
    public static class Routing
    {
        public static void Route(ItemDrop.ItemData item)
        {
            if (!StowActions.Ready(out Player player) || Refused(player, item))
                return;
            string name = ItemNames.DisplayName(item);
            List<Container> nearby = StowTargets.Nearby(player);
            nearby.RemoveAll(container => StowRules.Refuses(container, item) || FeedOnly(container));
            Container target = FindTarget(nearby, item);
            if (target == null)
            {
                Messages.Center(StowWords.Format(StowWords.NoRoute, name));
                return;
            }
            Overflow.Send(player, item, item.m_stack, target, nearby, sent => Report(sent, SentWords(sent, name)), StowTargets.OpenTarget);
        }

        public static void StoreOne(ItemDrop.ItemData item)
        {
            if (!StowActions.Ready(out Player player) || Refused(player, item))
                return;
            string name = ItemNames.DisplayName(item);
            Container target = StowTargets.OpenTarget;
            if (target == null && StowTargets.ViewingOnly())
                return;
            List<Container> nearby = StowTargets.Nearby(player);
            nearby.RemoveAll(FeedOnly);
            target = target ?? Holder(nearby, item);
            if (target == null || StowRules.Refuses(target, item))
            {
                Messages.Center(StowWords.Format(StowWords.NoRoute, name));
                return;
            }
            Overflow.Send(player, item, 1, target, nearby, sent => Report(sent, StowWords.Format(StowWords.StoredOne, name)));
        }

        /// <summary>
        /// A Shepherd trough (BS1) that is not the open container: routing never picks it. The open container stays a
        /// target because the player chose it.
        /// </summary>
        private static bool FeedOnly(Container container) =>
            container != StowTargets.Open && Burrow.Integration.IntegrationLinks.IsFeedContainer(container);

        /// <summary>
        /// "Sent x to chest", or "Sent x to chest and n more" when the stack went on to further containers; the first
        /// container named is the one that took the first part.
        /// </summary>
        internal static string SentWords(Overflow sent, string name)
        {
            if (sent.Took.Count == 0)
                return "";
            string first = StowTargets.Name(sent.Took[0]);
            if (sent.Took.Count == 1)
                return StowWords.Format(StowWords.Routed, name, first);
            return StowWords.Format(StowWords.RoutedMore, name, first, sent.Took.Count - 1);
        }

        /// <summary>The message at the end of a route: the success words, else "Nothing to move" unless a shared chest's refusal was just said.</summary>
        private static void Report(Overflow sent, string success)
        {
            if (sent.Took.Count > 0)
                Messages.Center(success);
            else if (!sent.RefusalSaid)
                Messages.Center(StowWords.Nothing);
        }

        private static bool Refused(Player player, ItemDrop.ItemData item)
        {
            string blocker = Movable.Blocker(player, player.GetInventory(), item);
            if (blocker == null)
                return false;
            if (blocker.Length > 0)
                Messages.Center(StowWords.Format(blocker, ItemNames.DisplayName(item)));
            return true;
        }

        /// <summary>The first container of the list that holds the item (by name), or null.</summary>
        private static Container Holder(List<Container> nearby, ItemDrop.ItemData item)
        {
            return nearby.Find(container => container.GetInventory().ContainsItemByName(item.m_shared.m_name));
        }

        /// <summary>
        /// From the nearby targets without refusing ones (the open one first, then nearest first) that have room for one
        /// unit: a holder of the item, the open container, a holder of its group, or an accepting one. A full holder
        /// (a chest, an animal feeder) is passed over, so the item still finds the open chest.
        /// </summary>
        private static Container FindTarget(List<Container> nearby, ItemDrop.ItemData item)
        {
            List<Container> roomy = nearby.FindAll(container => HasRoom(container, item));
            Container exact = Holder(roomy, item);
            if (exact != null)
                return exact;
            Container open = StowTargets.OpenTarget;
            if (open != null && HasRoom(open, item) && !StowRules.Refuses(open, item))
                return open;
            List<string> groups = StowRules.GroupsOf(item);
            Container grouped = groups.Count > 0 ? roomy.Find(container => HoldsGroup(container, groups)) : null;
            return grouped ?? roomy.Find(container => StowRules.Accepts(container, item));
        }

        private static bool HasRoom(Container container, ItemDrop.ItemData item) => container.GetInventory().CanAddItem(item, 1);

        private static bool HoldsGroup(Container container, List<string> groups)
        {
            foreach (ItemDrop.ItemData held in container.GetInventory().GetAllItems())
            {
                foreach (string group in groups)
                {
                    if (StowRules.Groups.Matches(group, held))
                        return true;
                }
            }
            return false;
        }
    }
}
