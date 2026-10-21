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
        public static IReadOnlyList<PinPoint> Spread(IReadOnlyList<PinPoint> points, float gap, bool enabled) =>
            Layout(points, gap, enabled, null);

        /// <summary>The side of each pin's tag for <see cref="Spread"/> with the same arguments: below for a pin that keeps
        /// its place; for a spread pin the side facing away from its cluster's center (top, right, bottom or left), so
        /// the tags of a cluster fan out instead of overlapping.</summary>
        public static IReadOnlyList<LabelSide> LabelSides(IReadOnlyList<PinPoint> points, float gap, bool enabled)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var sides = new LabelSide[points.Count];
            Layout(points, gap, enabled, sides);
            return sides;
        }

        static IReadOnlyList<PinPoint> Layout(IReadOnlyList<PinPoint> points, float gap, bool enabled, LabelSide[]? sides)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            var result = new PinPoint[points.Count];
            for (int i = 0; i < points.Count; i++) result[i] = points[i];
            if (!enabled || points.Count < 2 || !(gap > 0f)) return result;

            var parent = new int[points.Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Root(int i)
            {
                while (parent[i] != i) i = parent[i] = parent[parent[i]];
                return i;
            }
            float limit = gap * gap;
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                {
                    float dx = points[i].X - points[j].X, dy = points[i].Y - points[j].Y;
                    if (dx * dx + dy * dy < limit) parent[Root(j)] = Root(i);
                }

            var clusters = new Dictionary<int, List<int>>();
            for (int i = 0; i < points.Count; i++)
            {
                int root = Root(i);
                if (!clusters.TryGetValue(root, out var members)) clusters[root] = members = new List<int>();
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
                    result[members[k]] = new PinPoint((float)(cx + radius * dx), (float)(cy + radius * dy));
                    if (sides != null)
                        sides[members[k]] = Math.Abs(dy) >= Math.Abs(dx) - 1e-6 ? (dy > 0 ? LabelSide.Above : LabelSide.Below) : (dx > 0 ? LabelSide.Right : LabelSide.Left);
                }
            }
            return result;
        }
    }
}
