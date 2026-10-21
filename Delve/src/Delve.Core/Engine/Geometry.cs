// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: Unity-free positions for the portable rules (the plugin converts UnityEngine.Vector3).
using System;

namespace ExtraPotions.Delve.Engine
{
    /// <summary>A world position in meters (x east, y up, z north).</summary>
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        /// <summary>Distance on the ground plane, height ignored.</summary>
        public static float Flat(Vec3 a, Vec3 b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static float SqrDistance(Vec3 a, Vec3 b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public bool Equals(Vec3 other) => X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        public override bool Equals(object? obj) => obj is Vec3 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();
        public override string ToString() => "(" + X.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ", "
            + Y.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ", " + Z.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + ")";
    }

    /// <summary>An axis-aligned volume, such as a dungeon interior high above its entrance.</summary>
    public readonly struct Box
    {
        public Box(Vec3 min, Vec3 max)
        {
            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z) throw new ArgumentException("Expected min below max on every axis.");
            Min = min;
            Max = max;
        }

        public Vec3 Min { get; }
        public Vec3 Max { get; }

        public bool Contains(Vec3 p) => p.X >= Min.X && p.X <= Max.X && p.Y >= Min.Y && p.Y <= Max.Y && p.Z >= Min.Z && p.Z <= Max.Z;
    }

    /// <summary>A flat circle on the ground, such as a ward or a player base area.</summary>
    public readonly struct Area
    {
        public Area(Vec3 center, float radius)
        {
            if (!(radius >= 0) || float.IsInfinity(radius)) throw new ArgumentException("Expected a finite radius of 0 or more.", nameof(radius));
            Center = center;
            Radius = radius;
        }

        public Vec3 Center { get; }
        public float Radius { get; }

        public bool Contains(Vec3 p) => Vec3.Flat(Center, p) <= Radius;
    }
}
