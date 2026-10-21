// Delve - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original: which catalog key a notice uses (design D1 to D4) and the per-player biome notice cooldown (D3),
// following the behavior RestockRegen's README describes.
using System;
using System.Collections.Generic;
using System.Linq;
using ExtraPotions.Delve.Engine;

namespace ExtraPotions.Delve.Localization
{
    /// <summary>Where a player is told about a dungeon site.</summary>
    public enum SiteNotice
    {
        /// <summary>Inside: the full delay ("after {0} without visitors").</summary>
        Inside,
        /// <summary>At the entrance: what is left ("in {0} if no one enters").</summary>
        Entrance,
        /// <summary>Due: at the next check once no one is near.</summary>
        Due,
    }

    public static class NoticeText
    {
        /// <summary>The duration phrase key and its argument: "1 day", "6 days", "1 hour", "12 hours".</summary>
        public static (string Key, int Count) Duration(ClockKind clock, int units)
        {
            if (clock == ClockKind.RealHours) return units == 1 ? (DelveTextKeys.HoursOne, 1) : (DelveTextKeys.HoursMany, units);
            return units == 1 ? (DelveTextKeys.DaysOne, 1) : (DelveTextKeys.DaysMany, units);
        }

        public static string Site(DelveKind kind, SiteNotice notice)
        {
            var stem = DelveTextKeys.SiteKinds.Where(s => s.Kind == kind).Select(s => s.Stem).FirstOrDefault()
                ?? throw new ArgumentException(kind + " has no site notices.", nameof(kind));
            return stem + "_" + notice.ToString().ToLowerInvariant();
        }

        public static string Biome(DelveKind kind) =>
            DelveTextKeys.BiomeKinds.Where(b => b.Kind == kind).Select(b => b.Key).FirstOrDefault()
            ?? throw new ArgumentException(kind + " has no biome notice.", nameof(kind));
    }

    /// <summary>
    /// A player is told about a biome's regeneration when they enter it, at most once per cooldown, so walking along a
    /// border does not repeat it. State is per player and biome; forget a player when they leave the server.
    /// </summary>
    public sealed class NoticeCooldown
    {
        private readonly Dictionary<(long, string), (bool Inside, double LastTold)> state = new Dictionary<(long, string), (bool, double)>();

        /// <summary>Records where the player is now; true when they should be told.</summary>
        public bool Enter(long player, string biome, bool inside, double nowMinutes, double cooldownMinutes)
        {
            var key = (player, biome ?? "");
            state.TryGetValue(key, out var s);
            bool known = state.ContainsKey(key);
            bool entering = inside && (!known || !s.Inside);
            bool tell = entering && (!known || double.IsNegativeInfinity(s.LastTold) || nowMinutes - s.LastTold >= cooldownMinutes);
            state[key] = (inside, tell ? nowMinutes : (known ? s.LastTold : double.NegativeInfinity));
            return tell;
        }

        public void Forget(long player)
        {
            foreach (var key in state.Keys.Where(k => k.Item1 == player).ToList()) state.Remove(key);
        }
    }
}
