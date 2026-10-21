// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 Pete Navarra (aka Vapok), MIT. Adapted from BetterSleepBruh 2.1.0 at
// fe2bb81866e58f3c4afeb832876cba6ebf1e12cd, BetterSleepBruh/Components/SleepTracker.cs (NotifyBedOccupancyChanged
// throttle, UpdateSleeping heartbeat and window edges, RecalculateOccupancy change detection).
// The MIT notice is retained in THIRD_PARTY_NOTICES/BetterSleepBruh-MIT.txt.
// Changed by expDARE, 2026: the recount throttle is per peer and admits only verified session peers (Crucible
// PeerCommandContext.ServerConnectionVerified) with a bounded peer table; window edges and the publish gate as small
// state classes over a portable SleepState; namespace Ember.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Ember.Sleep
{
    /// <summary>The sleep state the authority publishes to clients (reply of ember.sleep.state).</summary>
    public readonly struct SleepState : IEquatable<SleepState>
    {
        public bool WindowOpen { get; }
        public int Players { get; }
        public int Sleeping { get; }
        public double ExtraRate { get; }
        public bool Boosting { get; }
        public bool FullSkip { get; }

        /// <summary>Game seconds the partial boost has added in this world (station timers, wire version 2).</summary>
        public double BoostTotal { get; }
        public StationBoostTotals? StationTotals { get; }

        public SleepState(bool windowOpen, int players, int sleeping, double extraRate, bool boosting, bool fullSkip, double boostTotal = 0.0)
            : this(windowOpen, players, sleeping, extraRate, boosting, fullSkip, boostTotal, null) { }

        public SleepState(bool windowOpen, int players, int sleeping, double extraRate, bool boosting, bool fullSkip, double boostTotal, StationBoostTotals? stationTotals)
        {
            WindowOpen = windowOpen;
            Players = players;
            Sleeping = sleeping;
            ExtraRate = extraRate;
            Boosting = boosting;
            FullSkip = fullSkip;
            BoostTotal = boostTotal;
            StationTotals = stationTotals;
        }

        public SleepState WithBoostTotal(double boostTotal) => new SleepState(WindowOpen, Players, Sleeping, ExtraRate, Boosting, FullSkip, boostTotal);
        public SleepState WithStationTotals(double boostTotal, StationBoostTotals totals) => new SleepState(WindowOpen, Players, Sleeping, ExtraRate, Boosting, FullSkip, boostTotal, totals);

        public bool Equals(SleepState o) => WindowOpen == o.WindowOpen && Players == o.Players && Sleeping == o.Sleeping
            && ExtraRate.Equals(o.ExtraRate) && Boosting == o.Boosting && FullSkip == o.FullSkip && BoostTotal.Equals(o.BoostTotal) && Nullable.Equals(StationTotals, o.StationTotals);
        public override bool Equals(object? obj) => obj is SleepState o && Equals(o);
        public override int GetHashCode() => (Players * 397) ^ (Sleeping * 31) ^ ExtraRate.GetHashCode() ^ (WindowOpen ? 1 : 0) ^ (Boosting ? 2 : 0) ^ (FullSkip ? 4 : 0) ^ BoostTotal.GetHashCode();
        public override string ToString() => $"window={WindowOpen} {Sleeping}/{Players} rate={ExtraRate} boosting={Boosting} skip={FullSkip} boostTotal={BoostTotal}";
    }

    /// <summary>S9: a bed-changed hint triggers a recount at most once per 0.25 s per peer, and only from a verified peer.</summary>
    public sealed class RecountThrottle
    {
        public const double IntervalSeconds = 0.25;
        public const int MaxPeers = 256;

        readonly Dictionary<string, double> last = new Dictionary<string, double>(StringComparer.Ordinal);

        public int TrackedPeers => last.Count;

        /// <summary>True when the hint should trigger a recount now. The hint carries no counts; counts come from ZDOs.</summary>
        public bool Admit(string peerId, bool verified, double now)
        {
            if (!verified || string.IsNullOrEmpty(peerId))
                return false;
            if (last.TryGetValue(peerId, out var previous) && now - previous + 1e-9 < IntervalSeconds)
                return false;
            if (!last.ContainsKey(peerId) && last.Count >= MaxPeers)
                Prune(now);
            if (!last.ContainsKey(peerId) && last.Count >= MaxPeers)
                return false;
            last[peerId] = now;
            return true;
        }

        /// <summary>Drops a peer that left the session.</summary>
        public void Forget(string peerId)
        {
            if (peerId != null)
                last.Remove(peerId);
        }

        public void Reset() => last.Clear();

        void Prune(double now)
        {
            var stale = new List<string>();
            foreach (var pair in last)
                if (now - pair.Value >= IntervalSeconds)
                    stale.Add(pair.Key);
            foreach (var key in stale)
                last.Remove(key);
        }
    }

    /// <summary>S9: the 1 s server tick forces a state publish every third tick (3 s) inside the window.</summary>
    public sealed class Heartbeat
    {
        public const double TickSeconds = 1.0;
        public const int ForceEveryTicks = 3;
        int counter;

        public void Reset() => counter = 0;

        public bool Tick(bool windowOpen)
        {
            if (!windowOpen)
                return false;
            counter++;
            if (counter < ForceEveryTicks)
                return false;
            counter = 0;
            return true;
        }
    }

    /// <summary>Window edge seen by the server tick.</summary>
    public enum WindowEdge
    {
        None,
        Opened,

        /// <summary>The window ended without a full skip: clients wake their players (S10, Wake At Window End).</summary>
        ClosedWake,

        /// <summary>The window ended because the game is skipping to morning: nobody is woken.</summary>
        ClosedDuringSkip,
    }

    /// <summary>S9, S10: opening and closing edges of the sleep window.</summary>
    public sealed class WindowEdges
    {
        bool wasOpen;

        public bool IsOpen => wasOpen;

        public void Reset() => wasOpen = false;

        public WindowEdge Update(bool windowOpen, bool timeSkipping)
        {
            var edge = WindowEdge.None;
            if (windowOpen && !wasOpen)
                edge = WindowEdge.Opened;
            else if (!windowOpen && wasOpen)
                edge = timeSkipping ? WindowEdge.ClosedDuringSkip : WindowEdge.ClosedWake;
            wasOpen = windowOpen;
            return edge;
        }
    }

    /// <summary>S9: publish when players, sleepers or the rate (by more than 0.001) changed, or when forced.</summary>
    public sealed class PublishGate
    {
        public const double RateEpsilon = 0.001;
        bool any;
        int players, sleeping;
        double rate;

        public bool ShouldPublish(SleepState state, bool force)
        {
            bool changed = force || !any || state.Players != players || state.Sleeping != sleeping
                || Math.Abs(state.ExtraRate - rate) > RateEpsilon;
            if (!changed)
                return false;
            any = true;
            players = state.Players;
            sleeping = state.Sleeping;
            rate = state.ExtraRate;
            return true;
        }

        /// <summary>Forget the last publish (window closed), so the next state is always sent.</summary>
        public void Reset() => any = false;
    }
}
