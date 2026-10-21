using System;
using Burrow.Core;

namespace Burrow.Reach
{
    /// <summary>
    /// Feeding stations from containers with the game's own add paths. A station patch runs before the game's
    /// method: when the inventory has nothing the station takes, one unit of the first acceptable item is
    /// borrowed from the nearest container into the inventory (<see cref="Borrow"/>); the game's method then
    /// consumes it exactly as if the player had carried it (caps, messages, RPCs to the owner). Afterwards
    /// <see cref="Settle"/> compares the inventory count: if the game did not consume the unit, it goes back into a
    /// container. The unit is therefore never in two places and never nowhere. <see cref="Fill"/> repeats the
    /// game's method while the Fill modifier is held, <see cref="Pull"/> moves up to one stack into the inventory
    /// instead of feeding.
    /// </summary>
    public static class StationFeed
    {
        /// <summary>One borrowed unit: its identity and the inventory count before it was moved in.</summary>
        public sealed class Loan
        {
            public Loan(string name, int quality, int before)
            {
                Name = name;
                Quality = quality;
                Before = before;
            }

            public string Name { get; }
            public int Quality { get; }
            public int Before { get; }
        }

        private static bool filling;

        /// <summary>The interacting humanoid is the local player, station feeding is on and the station's prefab is not disabled.</summary>
        public static bool Wanted(Humanoid user, UnityEngine.Component station)
        {
            return user != null && user == Player.m_localPlayer && ReachRules.Active(ReachMode.Stations)
                && ReachRules.StationRuleFor(station).Enabled;
        }

        public static bool PullHeld => Keys.Held(ReachSettings.PullModifier);

        /// <summary>Moves one acceptable unit from the nearest container into the inventory; null when there is none or it does not fit.</summary>
        public static Loan Borrow(Humanoid user, Func<ItemDrop.ItemData, bool> accepts)
        {
            Inventory inventory = user.GetInventory();
            ItemDrop.ItemData sample = FirstAcceptable(accepts);
            if (sample == null)
                return null;
            Loan loan = new Loan(sample.m_shared.m_name, sample.m_quality, inventory.CountItems(sample.m_shared.m_name, sample.m_quality, false));
            int moved = ReachPull.Move(inventory, item => ReachCount.Matches(item, loan.Name, loan.Quality, true) && accepts(item), 1);
            return moved > 0 ? loan : null;
        }

        /// <summary>After the game ran: a borrowed unit the game did not consume returns to a container.</summary>
        public static void Settle(Humanoid user, Loan loan)
        {
            if (loan == null || user == null)
                return;
            Inventory inventory = user.GetInventory();
            if (inventory.CountItems(loan.Name, loan.Quality, false) <= loan.Before)
                return;
            if (!ReachPull.ReturnOne(inventory, loan.Name, loan.Quality))
                Plugin.Log.LogInfo($"Burrow: one {loan.Name} borrowed for a station stays in the inventory, no container took it back");
        }

        /// <summary>The Pull modifier: up to one stack of the first acceptable item goes into the inventory.</summary>
        public static bool Pull(Humanoid user, Func<ItemDrop.ItemData, bool> accepts)
        {
            ItemDrop.ItemData sample = FirstAcceptable(accepts);
            if (sample == null)
            {
                Messages.Center("$burrow_nothingtopull");
                return false;
            }
            string name = sample.m_shared.m_name;
            int stack = Math.Max(1, sample.m_shared.m_maxStackSize);
            int moved = ReachPull.Move(user.GetInventory(), item => ReachCount.Matches(item, name, -1, true) && accepts(item), stack);
            Messages.Center(moved > 0 ? $"$burrow_pulled {moved} {name}" : "$inventory_full");
            return moved > 0;
        }

        /// <summary>
        /// The Fill modifier: calls the game's add method again until it refuses or the cap is reached. The
        /// station's ownership is claimed first (as the game does for cooking stations) so its caps are read from
        /// live data instead of a stale copy while the RPCs are in flight.
        /// </summary>
        public static void Fill(ZNetView view, int cap, Func<bool> again)
        {
            if (filling || PullHeld || !Keys.Held(ReachSettings.FillModifier))
                return;
            filling = true;
            try
            {
                if (view != null && view.IsValid() && !view.IsOwner())
                    view.ClaimOwnership();
                for (int i = 0; i < cap && again(); i++)
                {
                }
            }
            finally
            {
                filling = false;
            }
        }

        private static ItemDrop.ItemData FirstAcceptable(Func<ItemDrop.ItemData, bool> accepts)
        {
            foreach (Container container in ReachCount.Containers())
            {
                ItemDrop.ItemData found = ReachCount.FirstIn(container, item => item.m_worldLevel >= Game.m_worldLevel && accepts(item));
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}
