// Ember - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2023 Pete Navarra (aka Vapok), MIT. Adapted from DoorOpenerBruh 2.0.10 at
// dafb2af2418710dd1f87907e8b30d4efb1b68a20, DoorOpenerBruh/Components/DoorStatus.cs (GetDistanceToPlayer,
// open and close distance defaults).
// The MIT notice is retained in THIRD_PARTY_NOTICES/DoorOpenerBruh-MIT.txt.
// Changed by expDARE, 2026: portable vector and box types instead of Unity Vector3 and Collider.bounds (the plugin
// passes the bounds of enabled, non-trigger colliders); distance range 0.25..20 m with Close raised to Open; namespace Ember.
using System;
using System.Collections.Generic;

namespace ExtraPotions.Ember.Doors
{
    public readonly struct Vec3
    {
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static double Distance(Vec3 a, Vec3 b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    /// <summary>An axis-aligned box (a collider's world bounds).</summary>
    public readonly struct Box
    {
        public Vec3 Min { get; }
        public Vec3 Max { get; }

        public Box(Vec3 min, Vec3 max)
        {
            Min = new Vec3(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y), Math.Min(min.Z, max.Z));
            Max = new Vec3(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y), Math.Max(min.Z, max.Z));
        }

        public Vec3 ClosestPoint(Vec3 p) => new Vec3(Clamp(p.X, Min.X, Max.X), Clamp(p.Y, Min.Y, Max.Y), Clamp(p.Z, Min.Z, Max.Z));

        static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }

    /// <summary>D2: distance from the player to a door.</summary>
    public static class DoorDistance
    {
        /// <summary>
        /// The smaller of the distance to the door's origin and the distance to the nearest point of any collider box, so
        /// long drawbridges never close under the player.
        /// </summary>
        public static double ToPlayer(Vec3 doorOrigin, IEnumerable<Box>? colliderBounds, Vec3 player)
        {
            double min = Vec3.Distance(doorOrigin, player);
            if (colliderBounds == null)
                return min;
            foreach (var box in colliderBounds)
            {
                double d = Vec3.Distance(box.ClosestPoint(player), player);
                if (d < min)
                    min = d;
            }
            return min;
        }
    }

    /// <summary>D1, design 4.3: open and close distance range and the Close raised to Open rule.</summary>
    public static class DoorDistances
    {
        public const float Min = 0.25f;
        public const float Max = 20f;

        /// <summary>Current Open and Close Distance defaults are this fraction of the earlier ones (October 20, 2026 playtest).</summary>
        public const float DefaultScale = 0.5f;

        public static (float Open, float Close) Normalize(float open, float close)
        {
            float o = Clamp(open);
            float c = Clamp(close);
            return (o, Math.Max(o, c));
        }

        static float Clamp(float value) => float.IsNaN(value) ? Min : Math.Max(Min, Math.Min(Max, value));
    }
}
