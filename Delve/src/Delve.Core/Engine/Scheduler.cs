// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: due selection, dry run and load spreading for one engine check (design B1, B7, B9, B12).
using System;
using System.Collections.Generic;
using System.Linq;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>One tracked site at one check: its trigger, the guard verdict and a one-shot regenerate-now flag.</summary>
    public sealed class Candidate
    {
        public Candidate(string siteId, DelveKind kind, Stamp since, GuardResult guard, bool regenerateNow = false)
        {
            if (string.IsNullOrWhiteSpace(siteId)) throw new ArgumentException("Expected a site id.", nameof(siteId));
            SiteId = siteId;
            Kind = kind;
            Since = since;
            Guard = guard;
            RegenerateNow = regenerateNow;
        }

        public string SiteId { get; }
        public DelveKind Kind { get; }

        /// <summary>The trigger the delay counts from (first opening, last visit, kill).</summary>
        public Stamp Since { get; }

        public GuardResult Guard { get; }

        /// <summary>Admin "regenerate now" (B9): skips the delay, never the guards.</summary>
        public bool RegenerateNow { get; }
    }

    public sealed class SchedulerOptions
    {
        public SchedulerOptions(int maxActions, bool dryRunAll, Func<DelveKind, (ClockKind Clock, double Delay)> clockOf, Func<DelveKind, bool> dryRunOf)
        {
            if (maxActions < 1) throw new ArgumentOutOfRangeException(nameof(maxActions), "Expected at least one action per check.");
            MaxActions = maxActions;
            DryRunAll = dryRunAll;
            ClockOf = clockOf ?? throw new ArgumentNullException(nameof(clockOf));
            DryRunOf = dryRunOf ?? throw new ArgumentNullException(nameof(dryRunOf));
        }

        public int MaxActions { get; }
        public bool DryRunAll { get; }
        public Func<DelveKind, (ClockKind Clock, double Delay)> ClockOf { get; }
        public Func<DelveKind, bool> DryRunOf { get; }
    }

    public sealed class PlannedAction
    {
        internal PlannedAction(Candidate site, bool dryRun, double overdue)
        {
            Site = site;
            DryRun = dryRun;
            Overdue = overdue;
        }

        public Candidate Site { get; }
        public string SiteId => Site.SiteId;
        public DelveKind Kind => Site.Kind;

        /// <summary>Report only; change nothing (B7).</summary>
        public bool DryRun { get; }

        /// <summary>How far past its delay the site is, in its clock's unit (regenerate now counts as the most overdue).</summary>
        public double Overdue { get; }
    }

    /// <summary>The result of one check: what acts now and why the rest waits.</summary>
    public sealed class SchedulePlan
    {
        internal SchedulePlan(IReadOnlyList<PlannedAction> actions, int waiting, int deferred, IReadOnlyDictionary<GuardResult, int> blocked)
        {
            Actions = actions;
            Waiting = waiting;
            Deferred = deferred;
            Blocked = blocked;
        }

        public IReadOnlyList<PlannedAction> Actions { get; }

        /// <summary>Sites whose delay has not passed.</summary>
        public int Waiting { get; }

        /// <summary>Due and clear sites left for the next check by the action limit (B12).</summary>
        public int Deferred { get; }

        /// <summary>Due sites held by a guard, per reason (every reason present, zero when none).</summary>
        public IReadOnlyDictionary<GuardResult, int> Blocked { get; }
    }

    public static class Scheduler
    {
        public static SchedulePlan Plan(IEnumerable<Candidate> sites, Stamp now, SchedulerOptions options)
        {
            if (sites == null) throw new ArgumentNullException(nameof(sites));
            if (options == null) throw new ArgumentNullException(nameof(options));
            var blocked = Enum.GetValues(typeof(GuardResult)).Cast<GuardResult>().Where(g => g != GuardResult.Clear).ToDictionary(g => g, g => 0);
            var ready = new List<PlannedAction>();
            var waiting = 0;
            foreach (var site in sites)
            {
                var (clock, delay) = options.ClockOf(site.Kind);
                var since = site.Since.CompletedWith(now);
                double overdue;
                if (site.RegenerateNow) overdue = double.MaxValue;
                else if (ClockRules.Due(clock, delay, since, now)) overdue = ClockRules.Elapsed(clock, since, now) - ClockRules.Clamp(clock, delay);
                else
                {
                    waiting++;
                    continue;
                }
                if (site.Guard != GuardResult.Clear)
                {
                    blocked[site.Guard]++;
                    continue;
                }
                ready.Add(new PlannedAction(site, options.DryRunAll || options.DryRunOf(site.Kind), overdue));
            }
            var ordered = ready.OrderByDescending(a => a.Overdue).ThenBy(a => a.SiteId, StringComparer.Ordinal).ToList();
            var acting = ordered.Take(options.MaxActions).ToList();
            return new SchedulePlan(acting, waiting, ordered.Count - acting.Count, blocked);
        }
    }
}
