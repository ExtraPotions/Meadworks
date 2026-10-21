// Threshold - ExtraPotions Meadworks portals.
// Portions from Wayfare 0.3.2 (Copyright (C) 2026 Gerald Glasgow), GPL-3.0-or-later, at
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1: Wayfare/Wayfare/src/Targeting/PlayerFavourites.cs (KeyOf and the
// legacy-entry rule of Load).
// Changed by expDARE, 2026: extracted into a Unity-free helper used for favourites, hub and destination identity;
// Mathf.RoundToInt replaced by Math.Round to even (same result); set parsing and formatting added.
// Copyright (C) 2026 expDARE (ExtraPotions Meadworks) for the changes. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExtraPotions.Threshold.Portals
{
    /// <summary>A portal's identity across world loads: its position rounded to the metre, "x/y/z". The game renumbers every
    /// ZDOID on each world load, so an id key would point at another portal, or none, after a server restart. Portals never
    /// move and two cannot stand within a metre of each other, and a rename keeps the key.</summary>
    public static class PortalKey
    {
        public static string Of(MapPoint position)
        {
            if (!position.IsFinite) throw new ArgumentException("Expected a finite position.", nameof(position));
            return Round(position.X) + "/" + Round(position.Y) + "/" + Round(position.Z);
        }

        /// <summary>Keys from a comma-separated set, in order, without duplicates. Entries that are not three integers
        /// (Wayfare's older ZDOID strings, "user:id") are dropped: they no longer name a portal.</summary>
        public static IReadOnlyList<string> ParseSet(string? raw)
        {
            var keys = new List<string>();
            if (string.IsNullOrEmpty(raw)) return keys;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var part in raw!.Split(','))
            {
                var entry = part.Trim();
                if (IsKey(entry) && seen.Add(entry)) keys.Add(entry);
            }
            return keys;
        }

        public static string FormatSet(IEnumerable<string> keys) =>
            string.Join(",", (keys ?? Enumerable.Empty<string>()).Where(IsKey).Distinct(StringComparer.Ordinal));

        public static bool IsKey(string? entry)
        {
            if (string.IsNullOrEmpty(entry)) return false;
            var bits = entry!.Split('/');
            return bits.Length == 3 && bits.All(b => int.TryParse(b, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        }

        private static string Round(float value) =>
            ((int)Math.Round(value, MidpointRounding.ToEven)).ToString(CultureInfo.InvariantCulture);
    }
}
