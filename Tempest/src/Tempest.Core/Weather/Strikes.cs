// Tempest - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: lightning plans and base sparing (design 1.2, 4.2; WildWeather behaviour only).
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Tempest.Events;

namespace ExtraPotions.Tempest.Weather
{
    /// <summary>One planned strike: seconds into its minute and the ground offset from the player (meters, x and z).</summary>
    public readonly struct PlannedStrike
    {
        public PlannedStrike(double second, float x, float z, double time)
        {
            Second = second;
            X = x;
            Z = z;
            Time = time;
        }

        public double Second { get; }
        public float X { get; }
        public float Z { get; }

        /// <summary>World time of the strike (set by <see cref="StrikePlanner.Due"/>; 0 in a bare minute plan).</summary>
        public double Time { get; }
    }

    /// <summary>
    /// Strikes are planned per player from the event seed, the minute of the Active phase and the player id, so every peer
    /// can derive the same plan; the player's own client executes them (design 1.2). Kinds with strikes only
    /// (<see cref="EventKinds.Strikes"/>: thunderstorms, Lightning Storms and Meteor Showers).
    /// </summary>
    public static class StrikePlanner
    {
        public const float MinimumDistance = 15;
        public const float MaximumDistance = 60;
        public const int MaximumPerMinute = 30;

        public static IReadOnlyList<PlannedStrike> ForMinute(int eventSeed, long minute, long playerId, int perMinute)
        {
            var count = Math.Max(0, Math.Min(MaximumPerMinute, perMinute));
            var list = new List<PlannedStrike>(count);
            var a = (ulong)(uint)eventSeed;
            var b = unchecked((ulong)minute * 0x100000001B3UL ^ (ulong)playerId);
            for (var i = 0; i < count; i++)
            {
                var second = SeededRandom.Unit(a, b, (ulong)(i * 3 + 1)) * 60;
                var angle = SeededRandom.Unit(a, b, (ulong)(i * 3 + 2)) * 2 * Math.PI;
                var u = SeededRandom.Unit(a, b, (ulong)(i * 3 + 3));
                var distance = Math.Sqrt(MinimumDistance * MinimumDistance + u * (MaximumDistance * MaximumDistance - MinimumDistance * MinimumDistance));
                list.Add(new PlannedStrike(second, (float)(Math.Cos(angle) * distance), (float)(Math.Sin(angle) * distance), 0));
            }
            return list.OrderBy(s => s.Second).ToList();
        }

        /// <summary>
        /// Strikes per minute for an event: a lightning storm multiplies the setting by <paramref name="lightningMultiplier"/>
        /// (rounded, 0 to <see cref="MaximumPerMinute"/>). The authority computes it from its own settings, so every order agrees.
        /// </summary>
        public static int Rate(TempestEvent e, int perMinute, float lightningMultiplier)
        {
            if (perMinute <= 0) return 0;
            if (e == null || !e.Dry || float.IsNaN(lightningMultiplier)) return Math.Min(MaximumPerMinute, perMinute);
            var m = Math.Max(1, Math.Min(5, lightningMultiplier));
            return (int)Math.Min(MaximumPerMinute, Math.Round(perMinute * m, MidpointRounding.AwayFromZero));
        }

        /// <summary>Strikes for this player with world times in (<paramref name="from"/>, <paramref name="to"/>], Active phase only.</summary>
        public static IReadOnlyList<PlannedStrike> Due(TempestEvent e, long playerId, int perMinute, double from, double to)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            var result = new List<PlannedStrike>();
            if (!EventKinds.Strikes(e.Kind) || !(to > from)) return result;
            var lo = Math.Max(from, e.ActiveStart - 1);
            var hi = Math.Min(to, e.ClearingStart);
            if (!(hi > lo)) return result;
            var firstMinute = (long)Math.Floor((lo - e.ActiveStart) / 60);
            var lastMinute = (long)Math.Floor((hi - e.ActiveStart) / 60);
            for (var m = Math.Max(0, firstMinute); m <= lastMinute; m++)
                foreach (var s in ForMinute(e.Seed, m, playerId, perMinute))
                {
                    var time = e.ActiveStart + m * 60 + s.Second;
                    if (time > from && time <= to && time < e.ClearingStart) result.Add(new PlannedStrike(s.Second, s.X, s.Z, time));
                }
            return result;
        }
    }

    /// <summary>A base or ward area: center and radius on the ground plane.</summary>
    public readonly struct SparedArea
    {
        public SparedArea(float x, float z, float radius)
        {
            X = x;
            Z = z;
            Radius = radius;
        }

        public float X { get; }
        public float Z { get; }
        public float Radius { get; }
    }

    /// <summary>A player-built piece position on the ground plane.</summary>
    public readonly struct PiecePoint
    {
        public PiecePoint(float x, float z)
        {
            X = x;
            Z = z;
        }

        public float X { get; }
        public float Z { get; }
    }

    /// <summary>W3: no strike lands inside a base or ward area, or within Base Clearance of a player-built piece.</summary>
    public static class BaseExclusion
    {
        public static bool Spared(float x, float z, IEnumerable<SparedArea> areas, IEnumerable<PiecePoint> pieces, float clearance)
        {
            foreach (var a in areas ?? Enumerable.Empty<SparedArea>())
                if (Within(x, z, a.X, a.Z, a.Radius)) return true;
            if (!(clearance > 0)) return false;
            foreach (var p in pieces ?? Enumerable.Empty<PiecePoint>())
                if (Within(x, z, p.X, p.Z, clearance)) return true;
            return false;
        }

        static bool Within(float x, float z, float cx, float cz, float r)
        {
            var dx = x - cx;
            var dz = z - cz;
            return dx * dx + dz * dz <= r * r;
        }
    }
}
