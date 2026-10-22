// Copyright (C) 2026 expDARE. SPDX-License-Identifier: GPL-3.0-or-later
using System;
namespace ExtraPotions.Barter.Trading
{
    public static class SellCursor
    {
        public static int Move(int current, int count, int direction) => count <= 0 ? -1 :
            Math.Max(0, Math.Min(count - 1, current < 0 ? 0 : current + Math.Sign(direction)));
    }
}
