// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Runtime
{
    public sealed partial class DelveEngine
    {
        bool CanReadSites() => System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThread
            && Plugin.Ready && ReferenceEquals(Plugin.Engine, this) && Running
            && ExtraPotions.Meadworks.Crucible.Plugin.Session.AuthorityReady
            && ReferenceEquals(CurrentWorld(), world) && DelveModules.ProductActive(out _);

        /// <summary>Up to 10000 known active nature resources, ordered by kind and record ID. No native scan or record writes.</summary>
        public IReadOnlyList<(string Id, DelveKind Kind, string Prefab, Vec3 Position,
            (ClockKind Clock, double? Remaining, bool Due, bool DryRun, bool Requested) Timing)> WorldSites()
        {
            if (!CanReadSites()) return Array.Empty<(string, DelveKind, string, Vec3, (ClockKind, double?, bool, bool, bool))>();
            var resources = Service<DelveWorldObjects>();
            if (resources == null) return Array.Empty<(string, DelveKind, string, Vec3, (ClockKind, double?, bool, bool, bool))>();
            var now = WorldClock.Now(ZNet.instance.GetTimeSeconds(), Math.Max(1, EnvMan.instance.m_dayLengthSec), DateTime.UtcNow);
            return resources.ReadSites(this, now).OrderBy(s => s.Kind).ThenBy(s => s.Id, StringComparer.Ordinal).Take(10000).ToArray();
        }

        /// <summary>Queues one current FullReset generator for one normal guarded evaluation. True means queued, not reset.</summary>
        public bool RequestDungeon(string siteId, bool skipDelay, Func<bool> stillAllowed)
        {
            if (stillAllowed == null || !CanReadSites() || !Active(DelveKind.FullReset)) return false;
            try { if (!stillAllowed()) return false; } catch { return false; }
            var full = Service<DelveFullReset>();
            if (full == null || !full.Request(this, siteId, skipDelay, stillAllowed)) return false;
            nextCheck = 0f;
            return true;
        }

        /// <summary>
        /// Copies currently known active resource/full-reset dungeon clocks on the Unity thread. No visits, guards,
        /// scheduling or world data are changed. Unknown active-clock stamps remain unknown; due does not mean eligible.
        /// </summary>
        public IReadOnlyList<DungeonSiteReport> DungeonSites()
        {
            if (!CanReadSites()) return Array.Empty<DungeonSiteReport>();
            var now = WorldClock.Now(ZNet.instance.GetTimeSeconds(), Math.Max(1, EnvMan.instance.m_dayLengthSec), DateTime.UtcNow);
            var sites = new List<DungeonSiteReport>();
            var resources = Service<DelveDungeons>();
            if (resources != null) sites.AddRange(resources.ReadSites(this, now));
            var full = Service<DelveFullReset>();
            if (full != null) sites.AddRange(full.ReadSites(this, now));
            return sites.OrderBy(s => s.Kind).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        }
    }
}
