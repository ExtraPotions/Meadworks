// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;

namespace ExtraPotions.Hex;

public static class SpawnProtectionGeometry
{
    public const double ChordError = .005;
    const double Epsilon = 1e-9;
    const int MaximumRegions = 512;

    public readonly struct Point
    {
        public Point(double x, double z) { X = x; Z = z; }
        public double X { get; }
        public double Z { get; }
    }

    public readonly struct Circle
    {
        public Circle(double x, double z, double radius)
        {
            if (!Finite(x) || !Finite(z) || !Finite(radius) || radius <= 0 || radius > 1024)
                throw new ArgumentOutOfRangeException(nameof(radius));
            X = x; Z = z; Radius = radius;
            Segments = Math.Max(12, (int)Math.Ceiling(Math.PI / Math.Acos(1 - Math.Min(ChordError, radius) / radius)));
        }
        public double X { get; }
        public double Z { get; }
        public double Radius { get; }
        public int Segments { get; }
        public double InnerRadius => Radius * Math.Cos(Math.PI / Segments);
    }

    public sealed class Region
    {
        internal Region(List<Point> points, bool covered) { Points = points; Covered = covered; }
        public IReadOnlyList<Point> Points { get; }
        public bool Covered { get; }
    }

    public static IReadOnlyList<Region> Partition(double x, double z, double size, IReadOnlyList<Circle> circles, Circle? extent = null, bool covered = false)
    {
        if (!Finite(x) || !Finite(z) || !Finite(size) || size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        var cell = new List<Point> { new(x, z), new(x + size, z), new(x + size, z + size), new(x, z + size) };
        return Partition(cell, circles, extent, covered);
    }

    public static IReadOnlyList<Region> PartitionTriangle(Point a, Point b, Point c, IReadOnlyList<Circle> circles, Circle? extent = null, bool covered = false)
    {
        double direction = Side(c, a, b);
        if (!Finite(a.X) || !Finite(a.Z) || !Finite(b.X) || !Finite(b.Z) || !Finite(c.X) || !Finite(c.Z) || Math.Abs(direction) <= Epsilon)
            throw new ArgumentOutOfRangeException(nameof(c));
        var triangle = direction > 0 ? new List<Point> { a, b, c } : new List<Point> { a, c, b };
        return Partition(triangle, circles, extent, covered);
    }

    public static double TriangleHeight(Point point, Point a, double heightA, Point b, double heightB, Point c, double heightC)
    {
        double area = Side(c, a, b);
        if (Math.Abs(area) <= Epsilon) throw new ArgumentOutOfRangeException(nameof(c));
        double weightA = Side(point, b, c) / area, weightB = Side(point, c, a) / area;
        return weightA * heightA + weightB * heightB + (1 - weightA - weightB) * heightC;
    }

    static IReadOnlyList<Region> Partition(List<Point> cell, IReadOnlyList<Circle> circles, Circle? extent, bool covered)
    {
        if (circles == null || circles.Count > 512) throw new ArgumentOutOfRangeException(nameof(circles));
        if (extent.HasValue) cell = Intersect(cell, extent.Value);
        var result = new List<Region>();
        if (covered) { if (Valid(cell)) result.Add(new Region(cell, true)); return result; }
        var uncovered = new List<List<Point>>();
        if (Valid(cell)) uncovered.Add(cell);
        foreach (var circle in circles)
        {
            var next = new List<List<Point>>();
            foreach (var polygon in uncovered)
            {
                if (Outside(polygon, circle)) { next.Add(polygon); continue; }
                if (Inside(polygon, circle)) { result.Add(new Region(polygon, true)); continue; }
                var rest = polygon;
                for (int edge = 0; edge < circle.Segments && Valid(rest); edge++)
                {
                    Edge(circle, edge, out var a, out var b);
                    var outside = Clip(rest, a, b, false);
                    if (Valid(outside)) next.Add(outside);
                    rest = Clip(rest, a, b, true);
                    if (next.Count + result.Count > MaximumRegions) throw new InvalidOperationException("Protection geometry limit");
                }
                if (Valid(rest)) result.Add(new Region(rest, true));
            }
            uncovered = next;
            if (uncovered.Count == 0) break;
        }
        foreach (var polygon in uncovered) result.Add(new Region(polygon, false));
        return result;
    }

    static List<Point> Intersect(List<Point> polygon, Circle circle)
    {
        if (Outside(polygon, circle)) return new List<Point>();
        if (Inside(polygon, circle)) return polygon;
        for (int edge = 0; edge < circle.Segments && Valid(polygon); edge++)
        {
            Edge(circle, edge, out var a, out var b);
            polygon = Clip(polygon, a, b, true);
        }
        return polygon;
    }

    static void Edge(Circle circle, int edge, out Point a, out Point b)
    {
        double angle = edge * Math.PI * 2 / circle.Segments, next = (edge + 1) * Math.PI * 2 / circle.Segments;
        a = new Point(circle.X + Math.Cos(angle) * circle.Radius, circle.Z + Math.Sin(angle) * circle.Radius);
        b = new Point(circle.X + Math.Cos(next) * circle.Radius, circle.Z + Math.Sin(next) * circle.Radius);
    }

    static bool Inside(List<Point> polygon, Circle circle)
    {
        double radius = circle.InnerRadius, squared = radius * radius;
        foreach (var point in polygon)
            if ((point.X - circle.X) * (point.X - circle.X) + (point.Z - circle.Z) * (point.Z - circle.Z) > squared) return false;
        return true;
    }

    static bool Outside(List<Point> polygon, Circle circle)
    {
        double minX = double.PositiveInfinity, minZ = minX, maxX = double.NegativeInfinity, maxZ = maxX;
        foreach (var point in polygon)
        {
            minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
            minZ = Math.Min(minZ, point.Z); maxZ = Math.Max(maxZ, point.Z);
        }
        double dx = Math.Max(minX - circle.X, Math.Max(0, circle.X - maxX));
        double dz = Math.Max(minZ - circle.Z, Math.Max(0, circle.Z - maxZ));
        return dx * dx + dz * dz >= circle.Radius * circle.Radius;
    }

    static List<Point> Clip(List<Point> polygon, Point a, Point b, bool inside)
    {
        var output = new List<Point>();
        if (polygon.Count == 0) return output;
        Point previous = polygon[polygon.Count - 1];
        double previousSide = Side(previous, a, b);
        bool previousKept = inside ? previousSide >= 0 : previousSide <= 0;
        foreach (var current in polygon)
        {
            double currentSide = Side(current, a, b);
            bool kept = inside ? currentSide >= 0 : currentSide <= 0;
            if (kept != previousKept)
            {
                double fraction = previousSide / (previousSide - currentSide);
                output.Add(new Point(previous.X + (current.X - previous.X) * fraction, previous.Z + (current.Z - previous.Z) * fraction));
            }
            if (kept) output.Add(current);
            previous = current; previousSide = currentSide; previousKept = kept;
        }
        return output;
    }

    static double Side(Point point, Point a, Point b) => (b.X - a.X) * (point.Z - a.Z) - (b.Z - a.Z) * (point.X - a.X);
    static bool Valid(List<Point> points) => points.Count >= 3 && Area(points) > Epsilon;
    static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    public static double Area(IReadOnlyList<Point> points)
    {
        double sum = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var next = points[(i + 1) % points.Count];
            sum += points[i].X * next.Z - next.X * points[i].Z;
        }
        return Math.Abs(sum) * .5;
    }
}
