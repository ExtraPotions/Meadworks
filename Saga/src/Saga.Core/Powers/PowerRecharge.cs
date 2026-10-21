// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Globalization;
namespace ExtraPotions.Saga.Powers
{
    public static class PowerRecharge
    {
        static float Valid(float seconds) => float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0 ? 0 : seconds;
        public static float Shared(float first, float next) => Math.Max(Valid(first), Valid(next));
        public static string TimeLeft(float seconds)
        {
            double total = Math.Ceiling(Valid(seconds));
            return Math.Floor(total / 60).ToString("0", CultureInfo.InvariantCulture) + ":" + (total % 60).ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
