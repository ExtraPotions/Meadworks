// Threshold - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Spread Clustered Pins (parity #70). PotalMap documents the behaviour only (pins of portals standing
// close together spread into a small circle so they do not overlap); this is an independent implementation.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Threshold.Map
{
    /// <summary>A pin position in map GUI pixels.</summary>
    public readonly struct PinPoint : IEquatable<PinPoint>
    {
        public PinPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public bool Equals(PinPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is PinPoint other && Equals(other);
        public override int GetHashCode() => (X, Y).GetHashCode();
        public override string ToString() => "(" + X + ", " + Y + ")";
    }

    /// <summary>Where a pin's tag sits: below it as the game's own pins, or, in a spread cluster, on the side away from
    /// the cluster's center.</summary>
    public enum LabelSide { Below, Above, Left, Right }

    public static class PinSpread
    {
        /// <summary>
        /// Pins closer than <paramref name="gap"/> pixels to another pin of their cluster (clusters chain through near
        /// neighbors) are set on a circle around the cluster's center, in input order clockwise from the top, so that
        /// neighbors on the circle stand <paramref name="gap"/> apart. Lone pins and everything with the setting off keep
        /// their places. Screen pixels, so the spread holds at every zoom.
        /// </summary>
        public static IReadOnlyList<PinPoint> Spread(IReadOnlyList<PinPoint> points, float gap, bool enabled)
        {
            var positions = new List<PinPoint>();
            var sides = new List<LabelSide>();
            LayoutInto(points, gap, enabled, positions, sides);
            return positions;
        }

        /// <summary>The side of each pin's tag for <see cref="Spread"/> with the same arguments: below for a pin that keeps
        /// its place; for a spread pin the side facing away from its cluster's center (top, right, bottom or left), so
        /// the tags of a cluster fan out instead of overlapping.</summary>
        public static IReadOnlyList<LabelSide> LabelSides(IReadOnlyList<PinPoint> points, float gap, bool enabled)
        {
            var positions = new List<PinPoint>();
            var sides = new List<LabelSide>();
            LayoutInto(points, gap, enabled, positions, sides);
            return sides;
        }

        /// <summary>How many times the reused scratch had to grow. A second layout of the same size does not grow it.</summary>
        public static int ScratchGrows { get; private set; }

        /// <summary>One layout into caller-owned buffers. Positions and sides are replaced. The cluster scratch is reused
        /// and grows only when a larger set arrives.</summary>
        public static void LayoutInto(IReadOnlyList<PinPoint> points, float gap, bool enabled, List<PinPoint> positions, List<LabelSide> sides)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            if (sides == null) throw new ArgumentNullException(nameof(sides));
            positions.Clear();
            sides.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                positions.Add(points[i]);
                sides.Add(LabelSide.Below);
            }
            if (!enabled || points.Count < 2 || !(gap > 0f)) return;

            EnsureParents(points.Count);
            for (int i = 0; i < points.Count; i++) parents[i] = i;
            int Root(int i)
            {
                while (parents[i] != i) i = parents[i] = parents[parents[i]];
                return i;
            }
            float limit = gap * gap;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                {
                    float dx = points[i].X - points[j].X, dy = points[i].Y - points[j].Y;
                    if (dx * dx + dy * dy < limit) parents[Root(j)] = Root(i);
                }

            clusters.Clear();
            poolUsed = 0;
            for (int i = 0; i < points.Count; i++)
            {
                int root = Root(i);
                if (!clusters.TryGetValue(root, out var members)) clusters[root] = members = Rent();
                members.Add(i);
            }
            foreach (var members in clusters.Values)
            {
                int n = members.Count;
                if (n < 2) continue;
                double cx = 0, cy = 0;
                foreach (var i in members) { cx += points[i].X; cy += points[i].Y; }
                cx /= n;
                cy /= n;
                // Neighbors on a circle of radius r stand 2 r sin(pi / n) apart.
                double radius = gap / (2.0 * Math.Sin(Math.PI / n));
                for (int k = 0; k < n; k++)
                {
                    double angle = Math.PI / 2.0 - 2.0 * Math.PI * k / n;
                    double dx = Math.Cos(angle), dy = Math.Sin(angle);
                    positions[members[k]] = new PinPoint((float)(cx + radius * dx), (float)(cy + radius * dy));
                    sides[members[k]] = Math.Abs(dy) >= Math.Abs(dx) - 1e-6 ? (dy > 0 ? LabelSide.Above : LabelSide.Below) : (dx > 0 ? LabelSide.Right : LabelSide.Left);
                }
            }
        }

        static int[] parents = Array.Empty<int>();
        static readonly Dictionary<int, List<int>> clusters = new Dictionary<int, List<int>>();
        static readonly List<List<int>> pool = new List<List<int>>();
        static int poolUsed;

        static void EnsureParents(int count)
        {
            if (parents.Length >= count) return;
            int size = parents.Length == 0 ? count : parents.Length;
            while (size < count) size *= 2;
            parents = new int[size];
            ScratchGrows++;
        }

        static List<int> Rent()
        {
            List<int> members;
            if (poolUsed < pool.Count)
                members = pool[poolUsed];
            else
            {
                members = new List<int>();
                pool.Add(members);
                ScratchGrows++;
            }
            poolUsed++;
            members.Clear();
            return members;
        }
    }
}
