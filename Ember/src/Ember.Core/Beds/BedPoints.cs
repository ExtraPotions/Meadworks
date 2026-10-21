// Adapted from OpenKeep/OpenKeep/src/Homestead/BedPoints.cs.
// Copyright (C) 2026 Gerald Glasgow. GPL-3.0-or-later.
// Pin: 684671f98de54db6ea6652092ec041f44016c37d
// Changed by expDARE, 2026: Unity-free points, finite validation and stored-set codec.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExtraPotions.Ember.Beds
{
    public static class BedPoints
    {
        public const float SameBed = 1f;

        public static bool Finite(BedPoint point) => Finite(point.X) && Finite(point.Y) && Finite(point.Z);

        public static string Format(BedPoint point)
        {
            if (!Finite(point)) throw new ArgumentOutOfRangeException(nameof(point));
            return Number(point.X) + ":" + Number(point.Y) + ":" + Number(point.Z);
        }

        public static bool TryParse(string? text, out BedPoint point)
        {
            point = default;
            string[] parts = (text ?? "").Split(':');
            if (parts.Length != 3 ||
                !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                return false;
            var parsed = new BedPoint(x, y, z);
            if (!Finite(parsed)) return false;
            point = parsed;
            return true;
        }

        public static bool Same(BedPoint a, BedPoint b)
        {
            if (!Finite(a) || !Finite(b)) return false;
            double dx = (double)a.X - b.X, dy = (double)a.Y - b.Y, dz = (double)a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz < SameBed * SameBed;
        }

        public static bool Contains(List<BedPoint> points, BedPoint point) => points.Exists(p => Same(p, point));

        // Dungeon height must not change which bed is nearest on the map.
        public static double MapDistance(BedPoint a, BedPoint b)
        {
            if (!Finite(a) || !Finite(b)) return double.PositiveInfinity;
            double dx = (double)a.X - b.X, dz = (double)a.Z - b.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public static void SortByDistance(List<BedPoint> points, BedPoint from)
        {
            points.Sort((a, b) => MapDistance(a, from).CompareTo(MapDistance(b, from)));
        }

        public static List<BedPoint> Decode(string? text)
        {
            var points = new List<BedPoint>();
            foreach (string member in (text ?? "").Split(','))
                if (TryParse(member, out var point) && !Contains(points, point)) points.Add(point);
            return points;
        }

        public static string Encode(IEnumerable<BedPoint> points)
        {
            var unique = new List<BedPoint>();
            var members = new List<string>();
            foreach (var point in points)
            {
                if (!Finite(point) || Contains(unique, point)) continue;
                unique.Add(point);
                members.Add(Format(point));
            }
            return string.Join(",", members);
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Number(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}
