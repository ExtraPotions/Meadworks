// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using UnityEngine;

namespace ExtraPotions.Inscribe.Adopted.Core
{
    /// <summary><c>#RRGGBB</c> ↔ <see cref="Color32"/>, our own parse (no culture, no named colors).</summary>
    public static class Colors
    {
        public static bool TryParse(string? text, out Color32 color)
        {
            color = new Color32(255, 255, 255, 255);
            if (text == null || text.Length != 7 || text[0] != '#')
            {
                return false;
            }
            if (!Hex(text, 1, out byte r) || !Hex(text, 3, out byte g) || !Hex(text, 5, out byte b))
            {
                return false;
            }
            color = new Color32(r, g, b, 255);
            return true;
        }

        private static bool Hex(string text, int at, out byte value)
        {
            return byte.TryParse(text.Substring(at, 2), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out value);
        }
    }
}
