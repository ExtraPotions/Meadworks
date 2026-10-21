// Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace Mantle.Presentation
{
    public static class WildbloodPlateReading
    {
        public static bool Valid(Tuple<bool, string, string, string> raw) => raw != null && raw.Item1 &&
            Line(raw.Item2, 128) && raw.Item2.Length > 0 && Line(raw.Item3, 128) && raw.Item3.Length > 0 && Line(raw.Item4, 1024);
        /// <summary>
        /// The world level in Wildblood's HUD status line (capability meadworks.wildblood.hud, Item1: "World tier 3", localized as
        /// "... {0}", tier 0 included): its last run of digits, or null with no tier shown. shortcut: Wildblood exposes no number,
        /// only the localized line; read a numeric capability instead once Wildblood adds one.
        /// </summary>
        public static string HudTier(Tuple<string, string> raw)
        {
            string line = raw?.Item1;
            // The HUD asks every frame and the line rarely changes: the same line gives the same answer without a new string.
            if (line != null && line == lastLine) return lastTier;
            string tier = Tier(line);
            if (line != null) { lastLine = line; lastTier = tier; }
            return tier;
        }

        static string lastLine, lastTier;

        static string Tier(string line)
        {
            if (line == null || !Line(line, 256)) return null;
            int end = line.Length;
            while (end > 0 && (line[end - 1] < '0' || line[end - 1] > '9')) end--;
            int start = end;
            while (start > 0 && line[start - 1] >= '0' && line[start - 1] <= '9') start--;
            return end > start && end - start <= 4 ? line.Substring(start, end - start) : null;
        }
        static bool Line(string text, int limit)
        {
            if (text == null || text.Length > limit) return false;
            foreach (var c in text) if (char.IsControl(c) || c == '\u2028' || c == '\u2029') return false;
            return true;
        }
    }
}
