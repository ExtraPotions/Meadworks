// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Restocker.cs Sweep (the per-chest decision) and DaysLeft). The MIT notice is in THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: the decision is a pure function over facts the plugin reads from the chest's ZDO; the stamp is
// a Delve two-clock Stamp instead of an integer day + 1, so a chest can wait in in-game days or real hours (design B1);
// the Notifier's in-memory clock start arrives as the Seen fact; writes, dry-run memory and the loot roll stay in the plugin.
// A loaded chest never returns Restock: Restock Loaded only keeps its clock running until the chest unloads.
using System;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Chests
{
    /// <summary>What the sweep knows about one loot chest (a prefab with a loot table, not excluded).</summary>
    public sealed class ChestFacts
    {
        public ChestFacts(int? itemCount, Stamp? stamp, Stamp? seen, bool playerBuilt, bool heldByPlayer, bool inUse, bool liveInventory = false)
        {
            ItemCount = itemCount;
            Stamp = stamp;
            Seen = seen;
            PlayerBuilt = playerBuilt;
            HeldByPlayer = heldByPlayer;
            InUse = inUse;
            LiveInventory = liveInventory;
        }

        /// <summary>Items in the chest; null when it was never loaded (the game rolls its loot on first load).</summary>
        public int? ItemCount { get; }

        /// <summary>The chest's clock, when one is running.</summary>
        public Stamp? Stamp { get; }

        /// <summary>When a player was seen opening or emptying it before the sweep stamped it.</summary>
        public Stamp? Seen { get; }

        /// <summary>Built by a player (has a creator): never ours.</summary>
        public bool PlayerBuilt { get; }

        /// <summary>A connected player has it loaded (<see cref="WorldRules.HeldByPlayer"/>).</summary>
        public bool HeldByPlayer { get; }

        /// <summary>Someone has it open now.</summary>
        public bool InUse { get; }

        /// <summary>
        /// A live container may still show the previous items: a player is in its active area, or this machine has the container loaded.
        /// </summary>
        public bool LiveInventory { get; }
    }

    public sealed class ChestOptions
    {
        public ChestOptions(ClockKind clock, double delay, bool resetOpened, bool restockLoaded)
        {
            Clock = clock;
            Delay = ClockRules.Clamp(clock, delay);
            ResetOpened = resetOpened;
            RestockLoaded = restockLoaded;
        }

        public ClockKind Clock { get; }
        public double Delay { get; }
        public bool ResetOpened { get; }
        public bool RestockLoaded { get; }
    }

    public enum ChestAction
    {
        /// <summary>Nothing to do (an untouched chest, or leftovers with no clock).</summary>
        None,
        SkipPlayerBuilt,
        SkipNeverLoaded,
        /// <summary>A live inventory still has the old items, or Restock Loaded is off: the item blob stays.</summary>
        SkipLoaded,
        /// <summary>Leftovers without Reset Opened: the running clock stops.</summary>
        ClearStamp,
        /// <summary>The clock starts (<see cref="ChestDecision.NewStamp"/>); not due yet.</summary>
        Stamp,
        /// <summary>Clocked and not due yet.</summary>
        Waiting,
        /// <summary>Replace the contents with a fresh roll, then stop the clock.</summary>
        Restock,
    }

    public sealed class ChestDecision
    {
        internal ChestDecision(ChestAction action, Stamp? newStamp = null, bool forced = false, bool keepOpening = false)
        {
            Action = action;
            NewStamp = newStamp;
            Forced = forced;
            KeepOpening = keepOpening;
        }

        public ChestAction Action { get; }

        /// <summary>The clock this sweep started, when it started one.</summary>
        public Stamp? NewStamp { get; }

        /// <summary>The clock was considered while a live inventory still held the old items. Those items were not replaced.</summary>
        public bool Forced { get; }

        /// <summary>Clear the stored clock and still remember that a player opened this chest.</summary>
        public bool KeepOpening { get; }

        /// <summary>A refill stops the clock until the chest is next opened or emptied.</summary>
        public bool ClearsStamp => Action == ChestAction.Restock || Action == ChestAction.ClearStamp;
    }

    /// <summary>
    /// The daily sweep's decision for one loot chest. No clock yet and empty or opened by a player: start its clock, from
    /// when the opening was seen if it was, otherwise now. Clock started the delay or more ago: replace whatever is inside
    /// with a fresh roll and stop the clock. With Reset Opened a chest with leftovers keeps its clock; without it only
    /// empty chests are on a clock and anything put back stops it. Restock Loaded lets that clock run while a container
    /// is still loaded, and the new roll is written on the first check after that copy is gone. Off leaves a loaded chest
    /// alone: a clock it already has is cleared, and the delay starts at the first check after it unloads.
    /// </summary>
    public static class ChestRefill
    {
        /// <summary>The item blob may be replaced only when no live container can still show the previous items.</summary>
        public static bool MayReplaceItems(bool heldByPlayer, bool liveInventory) => !heldByPlayer && !liveInventory;

        public static ChestDecision Decide(ChestFacts chest, Stamp now, ChestOptions options, bool regenerateNow = false)
        {
            if (chest == null) throw new ArgumentNullException(nameof(chest));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (chest.PlayerBuilt) return new ChestDecision(ChestAction.SkipPlayerBuilt);
            if (chest.ItemCount == null) return new ChestDecision(ChestAction.SkipNeverLoaded);
            bool live = !MayReplaceItems(chest.HeldByPlayer, chest.LiveInventory);
            bool forced = false;
            if (live && !options.RestockLoaded)
                return chest.Stamp.HasValue
                    ? new ChestDecision(ChestAction.ClearStamp, keepOpening: true)
                    : new ChestDecision(ChestAction.SkipLoaded);
            if (live)
            {
                if (chest.InUse) return new ChestDecision(ChestAction.SkipLoaded);
                forced = true;
            }
            int count = chest.ItemCount.Value;
            if (count > 0 && !options.ResetOpened)
                return new ChestDecision(chest.Stamp.HasValue ? ChestAction.ClearStamp : ChestAction.None, null, forced);
            var stamp = chest.Stamp;
            Stamp? started = null;
            if (!stamp.HasValue)
            {
                if (count > 0 && !chest.Seen.HasValue) return new ChestDecision(ChestAction.None, null, forced);
                var start = !options.RestockLoaded
                    ? now
                    : chest.Seen.HasValue && !After(chest.Seen.Value, now) ? chest.Seen.Value.CompletedWith(now) : now;
                started = start;
                stamp = start;
            }
            if (!regenerateNow && !ClockRules.Due(options.Clock, options.Delay, stamp.Value.CompletedWith(now), now))
                return new ChestDecision(started.HasValue ? ChestAction.Stamp : ChestAction.Waiting, started, forced);
            if (live) return new ChestDecision(started.HasValue ? ChestAction.Stamp : ChestAction.SkipLoaded, started, true);
            return new ChestDecision(ChestAction.Restock, started, forced);
        }

        /// <summary>
        /// Whole clock units until the chest's refill, from its stamp or, before the first sweep has stamped it, from when
        /// it was seen opened or empty; the full delay otherwise. 0 means due: it refills at the next sweep that finds
        /// nobody holding it.
        /// </summary>
        public static int UnitsLeft(ChestFacts chest, Stamp now, ChestOptions options)
        {
            if (chest == null) throw new ArgumentNullException(nameof(chest));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var start = chest.Stamp ?? chest.Seen;
            if (!start.HasValue) return ClockRules.WholeUnitsLeft(options.Delay);
            return ClockRules.WholeUnitsLeft(ClockRules.Remaining(options.Clock, options.Delay, start.Value.CompletedWith(now), now));
        }

        private static bool After(Stamp a, Stamp b) => a.HasDay && b.HasDay ? a.Day > b.Day : a.HasUtc && b.HasUtc && a.Utc > b.Utc;
    }
}
