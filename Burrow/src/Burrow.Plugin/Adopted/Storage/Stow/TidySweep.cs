using System.Collections.Generic;
using Burrow.Core;
using Burrow.Shared;

namespace Burrow.Stow
{
    /// <summary>
    /// One look at one chest, on the client that owns it: the chest's memory takes in what it holds now and what
    /// players put in by hand, then every stray it holds goes to the nearest home with room
    /// (<see cref="TidyChests.Homes"/>), and on to the next when that one fills up. At most <see cref="MaxStacks"/>
    /// stacks move per look, so a big junk chest empties over a few looks rather than in one frame. Items move only
    /// between chests this client owns, with the game's own inventory methods, so no other client ever writes the same
    /// chest at once; when the nearest home belongs to another client it is asked to hand that chest over
    /// (<see cref="HandOver"/>, only when the home has room for the item) and the item waits for the next look. Both
    /// chests are saved by the game after each change. The memory is
    /// written last and the chest's own read marker moved past it, so the owner does not read its whole inventory back.
    /// </summary>
    internal static class TidySweep
    {
        public const int MaxStacks = 8;

        /// <summary>True while Auto Tidy itself moves stacks, so the inventories' change callbacks are not taken for a player's change.</summary>
        public static bool Moving { get; private set; }

        /// <summary>
        /// Looks at a chest this client owns. Returns false when it could not be looked at now; <paramref name="moved"/>
        /// is the stacks moved out, <paramref name="waiting"/> is true when strays stayed for want of a home with room.
        /// </summary>
        public static bool Run(Container source, out int moved, out bool waiting)
        {
            moved = 0;
            waiting = false;
            if (!TidyChests.IsReady(source) || !source.m_nview.IsOwner() || !ContainerScan.Claim(source))
                return false;
            TidyThemes.Refresh();
            ZDO zdo = source.m_nview.GetZDO();
            TidyMemory memory = TidyMemory.Read(zdo);
            bool changed = memory.Update(TidyProfiles.Held(source.GetInventory()), TidyHands.Added(source), TidyProfiles.Now,
                TidyProfiles.FadeSeconds, TidyProfiles.SettleSeconds);
            TidyHands.Clear(source);
            moved = Strays(source, memory, TidyProfiles.Build(source, memory), ref waiting);
            if (changed || moved > 0)
                WriteMemory(source, zdo, memory);
            return true;
        }

        /// <summary>
        /// Writes the memory into the ZDO. The inventory there is unchanged by it, so when the chest had read (or saved)
        /// the ZDO's latest data before, its read marker moves along and the game does not load the same items again.
        /// </summary>
        private static void WriteMemory(Container source, ZDO zdo, TidyMemory memory)
        {
            uint before = zdo.DataRevision;
            memory.Write(zdo);
            if (source.m_nview.IsOwner() && source.m_lastRevision == before)
                source.m_lastRevision = zdo.DataRevision;
        }

        /// <summary>Sends each stray prefab the chest holds to its homes until the stack budget is spent.</summary>
        private static int Strays(Container source, TidyMemory memory, TidyProfile profile, ref bool waiting)
        {
            int moved = 0;
            foreach (string prefab in StraysHeld(source, profile))
            {
                if (moved >= MaxStacks)
                {
                    waiting = true;
                    break;
                }
                int stacks = Settle(source, prefab, profile.Share(prefab), MaxStacks - moved, ref waiting);
                if (stacks > 0)
                    memory.Sent(prefab, TidyProfiles.Now);
                moved += stacks;
            }
            return moved;
        }

        /// <summary>The stray prefabs among the items the chest holds, in the order they first appear.</summary>
        private static List<string> StraysHeld(Container source, TidyProfile profile)
        {
            List<string> strays = new List<string>();
            foreach (ItemDrop.ItemData item in source.GetInventory().GetAllItems())
            {
                string prefab = ItemNames.PrefabName(item);
                if (!strays.Contains(prefab) && profile.IsStray(prefab))
                    strays.Add(prefab);
            }
            return strays;
        }

        /// <summary>Sends one prefab to its homes, nearest first, until it is all placed or the budget is spent.</summary>
        private static int Settle(Container source, string prefab, float share, int budget, ref bool waiting)
        {
            int moved = 0;
            foreach (Container home in TidyChests.Homes(source, prefab, share))
            {
                if (!TidyChests.IsReady(home))
                    continue;
                if (!home.m_nview.IsOwner())
                {
                    if (HasRoom(source, home, prefab) && HandOver.Ask(home, HandOver.BackgroundSeconds))
                        TidySchedule.Soon(source);
                    waiting = true;
                    return moved;
                }
                moved += MoveStacks(source, home, prefab, budget - moved, out bool left);
                if (!left)
                    return moved;
                if (moved >= budget)
                    break;
            }
            waiting = true;
            return moved;
        }

        /// <summary>The home (in our copy) has room for a unit of the prefab's first stack in the source: worth asking for.</summary>
        private static bool HasRoom(Container source, Container home, string prefab)
        {
            foreach (ItemDrop.ItemData item in source.GetInventory().GetAllItems())
            {
                if (ItemNames.PrefabName(item) == prefab)
                    return home.GetInventory().CanAddItem(item, 1);
            }
            return false;
        }

        /// <summary>
        /// Moves the prefab's stacks into the home until one does not fully go (home full or refusing it, or the budget
        /// spent); <paramref name="left"/> then tells the caller to try the next home with the rest. Both chests are held,
        /// and their single saves happen while <see cref="Moving"/> is still set.
        /// </summary>
        private static int MoveStacks(Container source, Container home, string prefab, int budget, out bool left)
        {
            left = true;
            if (budget <= 0 || !ContainerScan.Claim(home, HandOver.BackgroundSeconds))
                return 0;
            Moving = true;
            try
            {
                // Burrow keeps the game's save per change (OpenKeep 3.0.0 batches those saves; Burrow does not adopt that batching).
                return MoveHeld(source, home, prefab, budget, out left);
            }
            finally
            {
                Moving = false;
            }
        }

        private static int MoveHeld(Container source, Container home, string prefab, int budget, out bool left)
        {
            int stacks = 0;
            left = false;
            foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(source.GetInventory().GetAllItems()))
            {
                if (ItemNames.PrefabName(item) != prefab)
                    continue;
                int units = item.m_stack;
                int added = stacks < budget ? MoveStack(source, home, item) : 0;
                stacks += added > 0 ? 1 : 0;
                if (added < units)
                {
                    left = true;
                    break;
                }
            }
            Saved(source, home, stacks);
            return stacks;
        }

        /// <summary>One stack into the home as far as it fits, with the game's inventory methods; 0 when the home refuses it.</summary>
        private static int MoveStack(Container source, Container home, ItemDrop.ItemData item)
        {
            if (StowRules.Refuses(home, item))
                return 0;
            return ChestOps.Transfer(source.GetInventory(), item, item.m_stack, home.GetInventory(), ChestOps.NoSlot);
        }

        /// <summary>Marks both chests changed; the holds write each once when they end.</summary>
        private static void Saved(Container source, Container home, int stacks)
        {
            if (stacks <= 0)
                return;
            ContainerScan.Save(source);
            ContainerScan.Save(home);
            Plugin.Log.LogDebug($"Burrow: tidy moved {stacks} stacks from {ContainerScan.PrefabName(source)} to {ContainerScan.PrefabName(home)}");
        }
    }
}
