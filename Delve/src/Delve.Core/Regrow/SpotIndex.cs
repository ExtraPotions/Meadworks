// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (c) 2026 liekos47, MIT (RestockRegen 0.19.0 at aa179d9120b4b141fdb306825136ba05be0908b0,
// src/Regrow.cs Spot, Key, SpotKeyAt, Remember and the duplicate check of Load). The MIT notice is in
// THIRD_PARTY_NOTICES/RestockRegen-MIT.txt.
// Changed by expDARE, 2026: an instance per site instead of static state; prefabs by name; Unity-free positions; the
// drifting flag is passed in by the caller (the kind catalog).
using System;
using System.Collections.Generic;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Regrow
{
    /// <summary>A remembered spot: the whole object's prefab, position and rotation (Euler degrees).</summary>
    public readonly struct Spot
    {
        public Spot(string prefab, Vec3 position, Vec3 rotation)
        {
            Prefab = prefab ?? throw new ArgumentNullException(nameof(prefab));
            Position = position;
            Rotation = rotation;
        }

        public string Prefab { get; }
        public Vec3 Position { get; }
        public Vec3 Rotation { get; }
    }

    /// <summary>
    /// Every spot of one site, remembered from the first time its object is seen, whole or damaged (design B8). Keys are
    /// positions rounded to 10 cm; a position read back from a record can land in a neighbouring cell, so lookups search
    /// the cells around it, and a floating object matches the same prefab within <see cref="DriftRadius"/> metres flat.
    /// Without this an object would be counted twice and, once due, doubled.
    /// </summary>
    public sealed class SpotIndex
    {
        public const float DriftRadius = 6f;

        private readonly Dictionary<(int, int, int), Spot> spots = new Dictionary<(int, int, int), Spot>();

        public int Count => spots.Count;

        public IEnumerable<Spot> Spots => spots.Values;

        public static (int, int, int) Key(Vec3 p) =>
            ((int)Math.Round(p.X * 10f, MidpointRounding.ToEven), (int)Math.Round(p.Y * 10f, MidpointRounding.ToEven), (int)Math.Round(p.Z * 10f, MidpointRounding.ToEven));

        /// <summary>The key of the remembered spot at this position, allowing for rounding and drift; null when none.</summary>
        public (int, int, int)? KeyAt(Vec3 p, string wholePrefab, bool drifts)
        {
            if (drifts)
            {
                foreach (var kv in spots)
                    if (kv.Value.Prefab == wholePrefab && Vec3.Flat(kv.Value.Position, p) < DriftRadius) return kv.Key;
                return null;
            }
            var k = Key(p);
            if (spots.ContainsKey(k)) return k;
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    for (int dz = -2; dz <= 2; dz++)
                    {
                        var n = (k.Item1 + dx, k.Item2 + dy, k.Item3 + dz);
                        if (spots.TryGetValue(n, out var s) && Vec3.SqrDistance(s.Position, p) < 0.04f) return n;
                    }
            return null;
        }

        /// <summary>Remembers the spot unless one is already known there; true when it was new.</summary>
        public bool Remember(Spot spot, bool drifts)
        {
            if (KeyAt(spot.Position, spot.Prefab, drifts) != null) return false;
            spots[Key(spot.Position)] = spot;
            return true;
        }

        public bool TryGet((int, int, int) key, out Spot spot) => spots.TryGetValue(key, out spot);
    }
}
