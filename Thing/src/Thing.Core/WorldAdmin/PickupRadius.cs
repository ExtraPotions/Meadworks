// Thing - ExtraPotions Meadworks. Copyright (C) 2026 expDARE (ExtraPotions Meadworks). SPDX-License-Identifier: GPL-3.0-or-later
// Meadworks-original (user parity row 125, 2026-10-10): the admin's own item pickup radius. Valheim_Admin_Tool's admin
// pickup radius is a behaviour reference only (no code used).
using System;
using System.Globalization;

namespace ExtraPotions.Thing.WorldAdmin
{
    public static class PickupRadius
    {
        public const float Min = 1f, Max = 10f;

        /// <summary>A radius in meters from 1 to 10 (decimal point), or "reset" (null: back to the game's own radius).</summary>
        public static bool TryParse(string? text, out float? radius)
        {
            radius = null;
            var t = (text ?? "").Trim();
            if (t.Equals("reset", StringComparison.OrdinalIgnoreCase)) return true;
            if (!float.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value) || !(value >= Min && value <= Max)) return false;
            radius = value;
            return true;
        }

        /// <summary>The chosen radius is set again when the live one differs (a new character after death or a world change).</summary>
        public static bool Reapply(float? wanted, float live) => wanted.HasValue && Math.Abs(wanted.Value - live) > 0.001f;
    }
}
