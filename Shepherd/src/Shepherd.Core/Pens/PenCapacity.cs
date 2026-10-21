// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
namespace ExtraPotions.Shepherd.Pens
{
    public static class PenCapacity
    {
        // Keel and land pens both use zero to mean no additional pen limit.
        public static int Maximum(int native, int? pen) => pen.HasValue && pen.Value > 0 ? pen.Value : native;
    }
}
