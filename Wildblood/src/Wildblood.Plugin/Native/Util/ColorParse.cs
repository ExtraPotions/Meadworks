// SPDX-License-Identifier: GPL-3.0-or-later
// Portions Copyright (C) 2026 Gerald Glasgow, EliteCreaturesReborn 3.20.0, GPL-3.0-or-later
// ad278ecf323901c3645eb1f9f6fac6833a0f8cb1
// Changed by expDARE, 2026: Wildblood namespace and owned runtime integration.
using UnityEngine;

namespace ExtraPotions.Wildblood.Adopted.Util
{
    /// <summary>Parses the "#RRGGBB" hex strings the palette config holds into Unity colours.</summary>
    internal static class ColorParse
    {
        public static Color Hex(string text, Color fallback)
        {
            if (!string.IsNullOrWhiteSpace(text) && ColorUtility.TryParseHtmlString(text.Trim(), out Color color))
            {
                return color;
            }
            return fallback;
        }
    }
}
