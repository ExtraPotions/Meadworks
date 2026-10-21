// Portions Copyright (C) 2026 Gerald Glasgow. EliteCrafting 0.7.0 at ad278ecf323901c3645eb1f9f6fac6833a0f8cb1.
// SPDX-License-Identifier: GPL-3.0-or-later
// Changed by expDARE, 2026: Inscribe namespace; Crucible configuration, session and lifecycle boundaries.
using System.Globalization;

namespace ExtraPotions.Inscribe.Adopted.Core
{
    /// <summary>
    /// Culture-invariant number text, the only way numbers are written to or read from item data and YAML. A German
    /// client writing <c>6,5</c> would be garbage to every other peer, so nothing here ever uses the current culture.
    /// </summary>
    public static class Numbers
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        /// <summary>Format <c>0.##</c>: no exponent, no group separator, '.' as the decimal point, '-' for negatives.</summary>
        public static string Format(float value) => value.ToString("0.##", Invariant);

        public static string Format(int value) => value.ToString(Invariant);

        public static bool TryFloat(string? text, out float value)
        {
            value = 0f;
            return text != null && float.TryParse(text, NumberStyles.Float, Invariant, out value)
                && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>A double, for arithmetic that must match another implementation digit for digit (tier ladders).</summary>
        public static bool TryDouble(string? text, out double value)
        {
            value = 0d;
            return text != null && double.TryParse(text, NumberStyles.Float, Invariant, out value)
                && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        public static bool TryInt(string? text, out int value)
        {
            value = 0;
            return text != null && int.TryParse(text, NumberStyles.Integer, Invariant, out value);
        }

        /// <summary>How many digits follow the decimal point in a number's text (0 for "3", 1 for "1.5").</summary>
        public static int Decimals(string text)
        {
            int dot = text.IndexOf('.');
            if (dot < 0)
            {
                return 0;
            }
            int end = text.Length;
            while (end > dot + 1 && text[end - 1] == '0')
            {
                end--;
            }
            return end - dot - 1;
        }
    }
}
