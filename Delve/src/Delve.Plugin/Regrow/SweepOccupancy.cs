// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Delve.Regrow
{
    internal sealed class SweepOccupancy<TSite, TPlayer> where TSite : class
    {
        private readonly Func<IReadOnlyList<TPlayer>, HashSet<TSite>> project;
        private IReadOnlyList<TPlayer>? players;
        private HashSet<TSite>? occupied;
        private long geometryRevision;
        private float insideRadius;

        internal SweepOccupancy(Func<IReadOnlyList<TPlayer>, HashSet<TSite>> project) => this.project = project;

        internal bool Contains(TSite site, IReadOnlyList<TPlayer> snapshot, long revision, float radius)
        {
            if (occupied == null || !ReferenceEquals(players, snapshot) || geometryRevision != revision || !insideRadius.Equals(radius))
            {
                var next = project(snapshot);
                players = snapshot;
                geometryRevision = revision;
                insideRadius = radius;
                occupied = next;
            }
            return occupied.Contains(site);
        }
    }
}
