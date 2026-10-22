// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;

namespace ExtraPotions.Barter.Trading
{
    public static class ServiceRules
    {
        public static bool RepairPrice(int basePrice, int quality, float durability, float maximum, float factor, out int price)
        {
            price = 0;
            if (basePrice <= 0 || quality <= 0 || float.IsNaN(durability) || float.IsInfinity(durability) ||
                float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0 || durability < 0 || durability >= maximum ||
                float.IsNaN(factor) || float.IsInfinity(factor) || factor < 0) return false;
            double value = Math.Ceiling(basePrice * (double)quality * ((maximum - durability) / maximum) * factor);
            if (value > int.MaxValue) return false;
            price = Math.Max(1, (int)value);
            return true;
        }
    }
}
