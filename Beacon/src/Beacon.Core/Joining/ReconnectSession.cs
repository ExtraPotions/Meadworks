// Beacon - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (BC04b): reconnect after an unexpected disconnect with limits (design F8, section 6).
using System;
using ExtraPotions.Beacon.Servers;

namespace ExtraPotions.Beacon.Joining
{
    public enum ReconnectStep
    {
        /// <summary>No reconnect and nothing to say.</summary>
        None,
        /// <summary>A reconnect is due after <see cref="ReconnectPlan.DelaySeconds"/>.</summary>
        Scheduled,
        /// <summary>A running cycle ended (out of attempts, or a kick, ban or refusal during it).</summary>
        Stopped,
        /// <summary>The server locked Reconnect On Disconnect off; said once for the drop.</summary>
        Locked,
    }

    public readonly struct ReconnectPlan
    {
        public ReconnectPlan(ReconnectStep step, ReconnectDecision decision, int attempt, int limit, int delaySeconds)
        {
            Step = step;
            Decision = decision;
            Attempt = attempt;
            Limit = limit;
            DelaySeconds = delaySeconds;
        }

        public ReconnectStep Step { get; }
        public ReconnectDecision Decision { get; }

        /// <summary>The attempt that is scheduled, from 1; 0 otherwise.</summary>
        public int Attempt { get; }

        /// <summary>Setting Reconnect Attempts, clamped to 1..10.</summary>
        public int Limit { get; }

        public int DelaySeconds { get; }
    }

    /// <summary>
    /// One reconnect cycle toward one server. It never reconnects after a kick, ban, the player's own logout, a refusal or a
    /// first connection that failed; a failed reconnect attempt continues the cycle. A connection that held for
    /// <see cref="StableSeconds"/> restores the full budget. Times are monotonic seconds from the caller.
    /// </summary>
    public sealed class ReconnectSession
    {
        public const double StableSeconds = 60;

        int attempts;
        double connectedAt = -1;
        double due = -1;

        public ServerAddress? Target { get; private set; }

        public bool Pending => due >= 0;

        /// <summary>Seconds left before the scheduled reconnect, rounded up; 0 when none.</summary>
        public int SecondsLeft(double now) => due < 0 ? 0 : (int)Math.Ceiling(Math.Max(0, due - now));

        /// <summary>The game started a join: a join to another server ends the cycle.</summary>
        public void OnJoinStarted(ServerAddress? joining)
        {
            if (Target != null && joining != Target) Cancel();
        }

        public void OnConnected(double now) => connectedAt = now;

        public ReconnectPlan OnDisconnected(ServerAddress target, DisconnectKind kind, bool wasConnected, bool enabled, bool lockedByServer, int maxAttempts, double now)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            due = -1;
            if (Target != target)
            {
                Target = target;
                attempts = 0;
            }
            if (wasConnected && connectedAt >= 0 && now - connectedAt >= StableSeconds) attempts = 0;
            connectedAt = -1;
            var effective = kind == DisconnectKind.ConnectFailed && attempts > 0 ? DisconnectKind.Unexpected : kind;
            var limit = Math.Max(ReconnectRules.MinAttempts, Math.Min(ReconnectRules.MaxAttempts, maxAttempts));
            var decision = ReconnectRules.Decide(enabled, lockedByServer, effective, attempts, maxAttempts);
            if (decision == ReconnectDecision.Reconnect)
            {
                var delay = ReconnectRules.DelaySeconds(attempts);
                attempts++;
                due = now + delay;
                return new ReconnectPlan(ReconnectStep.Scheduled, decision, attempts, limit, delay);
            }
            var wasCycling = attempts > 0;
            Cancel();
            if (decision == ReconnectDecision.Locked && effective == DisconnectKind.Unexpected)
                return new ReconnectPlan(ReconnectStep.Locked, decision, 0, limit, 0);
            return new ReconnectPlan(wasCycling ? ReconnectStep.Stopped : ReconnectStep.None, decision, 0, limit, 0);
        }

        /// <summary>True once when the scheduled reconnect is due; the caller then starts the join.</summary>
        public bool Tick(double now)
        {
            if (due < 0 || now < due) return false;
            due = -1;
            return true;
        }

        /// <summary>Ends the cycle; true when a reconnect was scheduled or a cycle was running.</summary>
        public bool Cancel()
        {
            var had = due >= 0 || attempts > 0;
            due = -1;
            attempts = 0;
            connectedAt = -1;
            Target = null;
            return had;
        }
    }
}
