using System;
using System.Collections.Generic;
using Burrow.Core;

namespace Burrow.Shared
{
    /// <summary>
    /// The one writer of SPEC 9.3, for every module that changes a container. When the local client owns the
    /// chest or may claim it because nobody uses it (<see cref="CanWriteNow"/>), a call changes the inventory at
    /// once: claim, the game's inventory methods, save (<see cref="ChestSync"/>). When <c>Shared Chests</c> is
    /// <c>Full</c> and another player uses the chest (<see cref="IsShared"/>), a call sends a request to the
    /// owning client and applies the answer when it comes (<see cref="ChestAsk"/>). Every call reports through
    /// <c>done(bool ok)</c>, synchronously or when the reply arrives; every refusal shows the centre message.
    /// A chest that is neither writable now nor shared refuses with "The chest cannot be changed right now".
    /// A take while the local player's inventory is held (a shared payment or transfer settling) waits quietly
    /// and runs again once the hold ends, at most <see cref="HoldWaitSeconds"/>; after that it answers false.
    /// </summary>
    public static class ChestWriter
    {
        /// <summary>The longest a take waits for the player's inventory hold to end.</summary>
        internal const double HoldWaitSeconds = 2.0;

        private struct Waiting
        {
            public double Until;
            public Player Player;
            public Container Chest;
            public Action Again;
            public Action<bool> Done;
        }

        private static readonly List<Waiting> waiting = new List<Waiting>();

        internal static int WaitingTakes => waiting.Count;
        /// <summary>True when the local client may change the chest synchronously (owner, or claimable because nobody uses it).</summary>
        public static bool CanWriteNow(Container c)
        {
            ZNetView view = c != null ? c.m_nview : null;
            if (view == null || !view.IsValid() || c.GetInventory() == null)
                return false;
            return view.IsOwner() || !ContainerScan.InUseByAnother(c);
        }

        /// <summary>True when writes must go through requests (Full mode, another player uses it, otherwise usable).</summary>
        public static bool IsShared(Container c) => ContainerScan.IsShared(c);

        /// <summary>Takes <paramref name="amount"/> of a chest stack into the local player's inventory.</summary>
        public static void Take(Container c, ItemDrop.ItemData item, int amount, Action<bool> done)
        {
            TakeTo(c, item, amount, ChestOps.NoSlot, done);
        }

        /// <summary>Takes into a slot of the player's inventory when it is free or holds the same kind, else anywhere.</summary>
        internal static void TakeTo(Container c, ItemDrop.ItemData item, int amount, Vector2i slot, Action<bool> done)
        {
            done = done ?? Ignore;
            if (!Ready(c, out Player player) || item == null || amount <= 0 || !c.GetInventory().ContainsItem(item))
            {
                done(false);
                return;
            }
            Action<bool> answer = done;
            if (WaitForHold(player, c, () => TakeTo(c, item, amount, slot, answer), done))
                return;
            if (CanWriteNow(c))
                done(ChestSync.Take(c, item, amount, player, slot));
            else if (Shared(c))
                ChestAsk.Take(c, item, amount, player, slot, done);
            else
                done(false);
        }

        /// <summary>Puts <paramref name="amount"/> of a player inventory stack into the chest (any slot, or <paramref name="slot"/> when given).</summary>
        public static void Put(Container c, ItemDrop.ItemData item, int amount, Vector2i? slot, Action<bool> done)
        {
            done = done ?? Ignore;
            if (!Ready(c, out Player player) || item == null || amount <= 0 || !player.GetInventory().ContainsItem(item))
            {
                done(false);
                return;
            }
            Vector2i target = slot ?? ChestOps.NoSlot;
            if (CanWriteNow(c))
                done(ChestSync.Put(c, item, amount, player, target));
            else if (Shared(c))
                ChestAsk.Put(c, item, amount, player, target, done);
            else
                done(false);
        }

        /// <summary>Moves within the chest: onto an empty slot, onto the same kind, or a swap of whole stacks.</summary>
        public static void Move(Container c, Vector2i from, Vector2i to, int amount, Action<bool> done)
        {
            done = done ?? Ignore;
            if (!Ready(c, out _) || amount <= 0)
            {
                done(false);
                return;
            }
            if (CanWriteNow(c))
                done(ChestSync.Move(c, from, to, amount));
            else if (Shared(c))
                ChestAsk.Move(c, from, to, amount, done);
            else
                done(false);
        }

        /// <summary>The game's Take all: every stack that fits moves into the player's inventory.</summary>
        public static void TakeAll(Container c, Action<bool> done)
        {
            done = done ?? Ignore;
            if (!Ready(c, out Player player))
            {
                done(false);
                return;
            }
            Action<bool> answer = done;
            if (WaitForHold(player, c, () => TakeAll(c, answer), done))
                return;
            if (CanWriteNow(c))
                done(ChestSync.TakeAll(c, player));
            else if (Shared(c))
                ChestAsk.TakeAll(c, player, done);
            else
                done(false);
        }

        /// <summary>The game's Stack all: every unequipped inventory stack whose kind the chest holds moves into it.</summary>
        public static void StackAll(Container c, Action<bool> done)
        {
            done = done ?? Ignore;
            if (!Ready(c, out Player player))
            {
                done(false);
                return;
            }
            if (CanWriteNow(c))
                done(ChestSync.StackAll(c, player));
            else if (Shared(c))
                ChestAsk.StackAll(c, player, done);
            else
                done(false);
        }

        private static bool Held(Player player, Container chest) =>
            Burrow.Resources.NativeInventoryGuards.Held(player.GetInventory())
            || Burrow.Resources.NativeInventoryGuards.Held(chest.GetInventory());

        private static bool WaitForHold(Player player, Container chest, Action again, Action<bool> done)
        {
            if (!Held(player, chest))
                return false;
            if (waiting.Count >= 128) { done(false); return true; }
            waiting.Add(new Waiting { Until = UnityEngine.Time.realtimeSinceStartupAsDouble + HoldWaitSeconds,
                Player = player, Chest = chest, Again = again, Done = done });
            return true;
        }

        /// <summary>Runs waiting takes once their holds end; expires them without changing either inventory.</summary>
        internal static void Tick()
        {
            if (waiting.Count == 0)
                return;
            double now = UnityEngine.Time.realtimeSinceStartupAsDouble;
            Waiting[] due = waiting.ToArray();
            waiting.Clear();
            foreach (Waiting take in due)
            {
                try
                {
                    // A queued UI action belongs to this character and chest. Recheck after each take: a shared take
                    // can acquire a new hold before the next one. Expiry never runs a partially guarded transfer.
                    if (take.Player != Player.m_localPlayer || !Ready(take.Chest, out _) || take.Player.IsDead()
                        || take.Player.IsTeleporting() || now >= take.Until)
                        take.Done(false);
                    else if (Held(take.Player, take.Chest))
                        waiting.Add(take);
                    else
                        take.Again();
                }
                catch (Exception e) { Plugin.Log.LogWarning($"Burrow: a chest take kept during an inventory hold failed: {e.GetType().Name}"); }
            }
        }

        private static bool Ready(Container c, out Player player)
        {
            player = Player.m_localPlayer;
            if (player == null || c == null || c.GetInventory() == null)
                return false;
            return c.m_nview != null && c.m_nview.IsValid();
        }

        /// <summary>Shared, or the refusal message.</summary>
        private static bool Shared(Container c)
        {
            if (IsShared(c))
                return true;
            Messages.Center(SharedWords.Unavailable);
            return false;
        }

        private static void Ignore(bool ok)
        {
        }
    }
}
